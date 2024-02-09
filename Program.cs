using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNpgsqlDataSource(
    "Host=db;Username=admin;Password=123;Database=rinha;Max Auto Prepare=200;Minimum Pool Size=10;MaxPoolSize=100",
    dataSourceBuilderAction: a => { a.UseLoggerFactory(NullLoggerFactory.Instance); });

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapPost("/clientes/{id}/transacoes", async ([FromRoute] int id, [FromBody] TransactionRequest request, NpgsqlConnection conn) =>
{
    if (id is (< 1 or > 5))
        return Results.NotFound(new { error = "Id not found" });

    if (request.Descricao.Length == 0 || request.Descricao.Length > 10)
        return Results.UnprocessableEntity(new { descricaoInvalida = request.Descricao });

    if (request.Tipo != 'c' && request.Tipo != 'd')
        return Results.UnprocessableEntity(new { tipoInvalido = request.Tipo });

    if (request.Valor <= 0)
        return Results.UnprocessableEntity(new { valor = request.Valor });

    bool connected = false;
    while (!connected)
    {
        try
        {
            await conn.OpenAsync();
            connected = true;
        }
        catch (NpgsqlException)
        {
            Console.WriteLine("Trying to reconnect.");
            await Task.Delay(1_000);
        }
    }

    var result = new TransactionResult(0, 0);

    var cmd = new NpgsqlCommand($"INSERT INTO transaction (value, type, description, customer_id) VALUES ({request.Valor}, '{request.Tipo}', '{request.Descricao}', {id})", conn);
    cmd.ExecuteNonQuery();

    if (request.Tipo == 'c')
    {
        cmd = new NpgsqlCommand($"UPDATE customer SET balance = balance + {request.Valor} WHERE id = {id} RETURNING \"limit\", balance", conn);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result = new TransactionResult(reader.GetInt32(0), reader.GetInt32(1));
        }
        await reader.CloseAsync();
        await reader.DisposeAsync();
    }
    else if (request.Tipo == 'd')
    {
        var transaction = await conn.BeginTransactionAsync();
        cmd = new NpgsqlCommand($"UPDATE customer SET balance = balance - {request.Valor} WHERE id = {id} RETURNING \"limit\", balance", conn, transaction);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result = new TransactionResult(reader.GetInt32(0), reader.GetInt32(1));
        }
        await reader.CloseAsync();
        await reader.DisposeAsync();

        if (result.Balance < 0)
        {
            transaction.Rollback();
            await conn.DisposeAsync();
            return Results.UnprocessableEntity();
        }
        transaction.Commit();
    }

    await conn.DisposeAsync();

    return Results.Ok(new { limite = result.Limit, saldo = result.Balance });
});

app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id, NpgsqlConnection conn) =>
{
    if (id is (< 1 or > 5))
        return Results.NotFound(new { error = "Id not found" });

    bool connected = false;
    while (!connected)
    {
        try
        {
            await conn.OpenAsync();
            connected = true;
        }
        catch (NpgsqlException)
        {
            Console.WriteLine("Trying to reconnect.");
            await Task.Delay(1_000);
        }
    }

    try
    {

        var cmd = new NpgsqlCommand($"SELECT \"limit\" as limite, balance as saldo FROM customer WHERE id = {id}", conn);
        BalanceStatementResult? saldo = null;

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                saldo = new BalanceStatementResult(reader.GetInt32(0), reader.GetInt32(1));
            }
            await reader.CloseAsync();
            await reader.DisposeAsync();
        }
        cmd = new NpgsqlCommand($"SELECT value as valor, \"type\" as tipo, description as descricao, created_at as realizada_em FROM transaction WHERE customer_id = {id} ORDER BY id LIMIT 10", conn);

        var latestTransactions = new List<TransactionStatementResult>();

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var transaction = new TransactionStatementResult(reader.GetInt32(0), reader.GetChar(1), reader.GetString(2), reader.GetDateTime(3));
                latestTransactions.Add(transaction);
            }
            await reader.CloseAsync();
            await reader.DisposeAsync();
        }

        await conn.DisposeAsync();
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
            return Results.NotFound();
        }

    }

    await conn.DisposeAsync();
    return Results.Problem();
});

app.Run();

public record TransactionRequest(int Valor, char Tipo, string Descricao);
public record TransactionResult(int Limit, int Balance);
public record BalanceStatementResult(int Limite, int Saldo);
public record TransactionStatementResult(int Valor, char Tipo, string Descricao, DateTime Realizada_Em);
