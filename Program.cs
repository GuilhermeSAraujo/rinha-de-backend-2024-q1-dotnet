using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using rinha_de_backend_2024_q1_dotnet.Pools;
using rinha_de_backend_2024_q1_dotnet.Types;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

//var connectionString = builder.Configuration.GetConnectionString("prd");
var connectionString = builder.Configuration.GetConnectionString("local");

builder.Services.AddSingleton<CreditTransactionPool>();
builder.Services.AddSingleton<DebitTransactionPool>();
builder.Services.AddSingleton<BalanceSatementPool>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(60) });

var app = builder.Build();

var creditTransactionPool = app.Services.GetRequiredService<CreditTransactionPool>();
var debitTransactionPool = app.Services.GetRequiredService<DebitTransactionPool>();
var balanceSatementPool = app.Services.GetRequiredService<BalanceSatementPool>();

var fillCreditTransactionPoolTask = Task.Run(() => creditTransactionPool.FillPool());
var fillDebitTransactionPoolTask = Task.Run(() => debitTransactionPool.FillPool());
var fillBalanceSatementPoolTask = Task.Run(() => balanceSatementPool.FillPool());
await Task.WhenAll(fillCreditTransactionPoolTask, fillDebitTransactionPoolTask, fillBalanceSatementPoolTask);

app.MapPost("/clientes/{id}/transacoes", async Task<Results<Ok<PostTransactionResult>, NotFound, UnprocessableEntity>> (
    [FromRoute] int id,
    [FromBody] TransactionRequest request,
    DebitTransactionPool debitTransactionPool,
    CreditTransactionPool creditTransactionPool) =>
{
    if (id is (< 1 or > 5))
    {
        return TypedResults.NotFound();
    }

    if (string.IsNullOrEmpty(request.Descricao) || request.Descricao.Length > 10)
    {
        return TypedResults.UnprocessableEntity();
    }

    if (request.Tipo != 'c' && request.Tipo != 'd')
    {
        return TypedResults.UnprocessableEntity();
    }

    if (request.Valor <= 0 || request.Valor % 1 != 0)
    {
        return TypedResults.UnprocessableEntity();
    }

    var result = new PostTransactionResult(0, 0);

    NpgsqlCommand command = null;

    try
    {

        if (request.Tipo == 'd')
            command = debitTransactionPool.GetCommand();
        else
            command = creditTransactionPool.GetCommand();

        command.Parameters[0].Value = (int)request.Valor;
        command.Parameters[1].Value = id;
        command.Parameters[2].Value = request.Descricao;

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        command.Connection = connection;

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            if (reader.GetBoolean(2) is false)
            {
                return TypedResults.UnprocessableEntity();
            }

            result.Saldo = reader.GetInt32(0);
            result.Limite = reader.GetInt32(1);
        }
    }
    finally
    {
        command.Connection = null;

        if (request.Tipo == 'd')
            debitTransactionPool.ReturnCommand(command);
        else
            creditTransactionPool.ReturnCommand(command);
    }
    return TypedResults.Ok(result);
});

app.MapGet("/clientes/{id}/extrato", async Task<Results<Ok<BalanceStatementJson>, NotFound, UnprocessableEntity>> ([FromRoute] int id, BalanceSatementPool statementPool) =>
{
    if (id is (< 1 or > 5))
        return TypedResults.NotFound();

    var commands = statementPool.GetCommand();

    commands.TryGetValue("selectCustomer", out NpgsqlCommand? selectCustomerCommand);

    selectCustomerCommand.Parameters[0].Value = id;

    using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    selectCustomerCommand.Connection = connection;

    var balanceStatementResult = new BalanceStatementResult();
    using (var readerCustomer = await selectCustomerCommand.ExecuteReaderAsync())
    {
        if (!readerCustomer.HasRows)
        {
            await readerCustomer.CloseAsync();
            await readerCustomer.DisposeAsync();
            selectCustomerCommand.Connection = null;
            statementPool.ReturnCommand(commands);

            return TypedResults.NotFound();
        }

        while (await readerCustomer.ReadAsync())
        {
            balanceStatementResult.Limite = readerCustomer.GetInt32(0);
            balanceStatementResult.Total = readerCustomer.GetInt32(1);
            balanceStatementResult.Data_Extrato = DateTime.Now;
        }
    }

    commands.TryGetValue("selectTransaction", out NpgsqlCommand? selectTransactionsCommand);

    var transactions = new List<TransactionStatementResult>();

    selectTransactionsCommand.Parameters[0].Value = id;

    selectTransactionsCommand.Connection = connection;

    using (var readerTransactions = await selectTransactionsCommand.ExecuteReaderAsync())
    {
        if (readerTransactions.HasRows)
        {
            while (await readerTransactions.ReadAsync())
            {
                var t = new TransactionStatementResult(
                    readerTransactions.GetInt32(0),
                    readerTransactions.GetChar(1),
                    readerTransactions.GetString(2),
                    readerTransactions.GetDateTime(3));

                transactions.Add(t);
            }
        }
    }

    selectCustomerCommand.Connection = null;
    selectTransactionsCommand.Connection = null;
    statementPool.ReturnCommand(commands);

    return TypedResults.Ok(new BalanceStatementJson(balanceStatementResult, transactions));
});

app.Run();
