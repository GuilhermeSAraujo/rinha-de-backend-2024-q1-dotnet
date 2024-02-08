using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddNpgsqlDataSource(
//    "Host=db;Username=admin;Password=123;Database=rinha",
//    dataSourceBuilderAction: a => { a.UseLoggerFactory(NullLoggerFactory.Instance); });

builder.Services.AddTransient<IDbConnection>((sp) => new NpgsqlConnection("Host=db;Username=admin;Password=123;Database=rinha"));


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

app.MapPost("/clientes/{id}/transacoes", async (IDbConnection _dbConnection, [FromRoute] int id, [FromBody] TransactionRequest request) =>
{
    _dbConnection.Open();

    var transaction = _dbConnection.BeginTransaction();

    TransactionResult result = new(0, 0);

    try
    {
        await _dbConnection.ExecuteAsync(@"INSERT INTO transaction (value, type, description, customer_id) VALUES (@value, @type, @description, @customerId)",
            new { value = request.Valor, type = request.Tipo, description = request.Descricao, customerId = id }, transaction);
    }
    catch (PostgresException e)
    {
        if (e.SqlState == "23503")
        {
            Console.WriteLine("Foreign key constraint violation: {0}", e.Message);
            transaction.Rollback();
            _dbConnection.Close();
            _dbConnection.Dispose();
            return Results.NotFound();
        }
        throw;
    }

    if (request.Tipo == 'c')
    {
        result = await _dbConnection.QuerySingleAsync<TransactionResult>(@"UPDATE customer SET balance = balance + @value WHERE id = @id RETURNING ""limit"", balance",
            new { value = request.Valor, id });
    }
    else if (request.Tipo == 'd')
    {
        result = await _dbConnection.QuerySingleAsync<TransactionResult>(@"UPDATE customer SET balance = balance - @value WHERE id = @id RETURNING ""limit"", balance",
            new { value = request.Valor, id });
    }

    if (result.Balance < 0)
    {
        transaction.Rollback();
        _dbConnection.Close();
        _dbConnection.Dispose();
        return Results.UnprocessableEntity();
    }

    transaction.Commit();
    _dbConnection.Close();
    _dbConnection.Dispose();

    return Results.Ok(new { limite = result.Limit, saldo = result.Balance });
});

app.MapGet("/clientes/{id}/extrato", async (IDbConnection _dbConnection, [FromRoute] int id) =>
{
    _dbConnection.Open();

    try
    {
        var saldo = await _dbConnection.QueryFirstOrDefaultAsync<BalanceStatementResult>(@"SELECT ""limit"" as limite, balance as saldo FROM customer WHERE id = @id", new { id });

        var latestTransactions = await _dbConnection.QueryAsync<TransactionStatementResult>(@"
        SELECT value as valor, type as tipo, description as descricao, created_at as realizada_em
        FROM transaction
        WHERE customer_id = @id
        ORDER BY id
        LIMIT 10", new { id });


        _dbConnection.Close();
        _dbConnection.Dispose();

        return Results.Ok(new
        {
            saldo,
            ultimas_transacoes = latestTransactions
        });
    }
    catch (PostgresException e)
    {
        if (e.SqlState == "23503")
        {
            _dbConnection.Close();
            _dbConnection.Dispose();
            return Results.NotFound();
        }

    }

    return Results.Problem();
});

app.Run();

public record TransactionRequest(int Valor, char Tipo, string Descricao);
public record TransactionResult(int Limit, int Balance);
public record BalanceStatementResult(int Limite, int Saldo);
public record TransactionStatementResult(int Valor, char Tipo, string Descricao, DateTime Realizada_Em);
