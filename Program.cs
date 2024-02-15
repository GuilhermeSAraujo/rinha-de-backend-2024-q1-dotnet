using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using rinha_de_backend_2024_q1_dotnet;
using rinha_de_backend_2024_q1_dotnet.Types;

var builder = WebApplication.CreateSlimBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("prd");
//var connectionString = builder.Configuration.GetConnectionString("local");

builder.Services.AddSingleton<InsertTransactionPool>();
builder.Services.AddSingleton<BalanceSatementPool>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(60) });

var app = builder.Build();

app.MapPost("/clientes/{id}/transacoes", async Task<Results<Ok<PostTransactionResult>, NotFound, UnprocessableEntity>> (
    [FromRoute] int id,
    [FromBody] TransactionRequest request,
    InsertTransactionPool insertTransactionPool) =>
{
    if (id is (< 1 or > 5))
        return TypedResults.NotFound();

    if (string.IsNullOrEmpty(request.Descricao) || request.Descricao.Length > 10)
        return TypedResults.UnprocessableEntity();

    if (request.Tipo != 'c' && request.Tipo != 'd')
        return TypedResults.UnprocessableEntity();

    if (request.Valor <= 0 || request.Valor % 2 != 0) 
        return TypedResults.UnprocessableEntity();

    var result = new PostTransactionResult(0, 0);

    var transactionCmd = insertTransactionPool.GetCommand();

    transactionCmd.Parameters[0].Value = request.Tipo == 'c' ? request.Valor * -1 : request.Valor;
    transactionCmd.Parameters[1].Value = id;
    transactionCmd.Parameters[2].Value = request.Tipo;
    transactionCmd.Parameters[3].Value = request.Descricao;

    using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    transactionCmd.Connection = connection;

    using var reader = await transactionCmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        if (reader.GetBoolean(2) is false)
            return TypedResults.UnprocessableEntity();

        result.Saldo = reader.GetInt32(0);
        result.Limite = reader.GetInt32(1);
    }

    insertTransactionPool.ReturnCommand(transactionCmd);

    return TypedResults.Ok(result);
});

app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id, BalanceSatementPool statementPool) =>
{
    if (id is (< 1 or > 5))
        return Results.NotFound(new { error = "Id not found" });

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
            await connection.CloseAsync();
            await connection.DisposeAsync();
            await readerCustomer.CloseAsync();
            await readerCustomer.DisposeAsync();
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

    statementPool.ReturnCommand(commands);

    return TypedResults.Ok(new BalanceStatementJson(balanceStatementResult, transactions));
});

app.Run();
