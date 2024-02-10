namespace rinha_de_backend_2024_q1_dotnet.Types;

public record struct TransactionRequest(int Valor, char Tipo, string Descricao);
public record struct TransactionResult(int Limit, int Balance);
public record struct BalanceStatementResult(int Limite, int Total, DateTime Data_Extrato);
public record struct TransactionStatementResult(int Valor, char Tipo, string Descricao, DateTime Realizada_Em);
public record struct BalanceStatementJson(BalanceStatementResult? saldo, List<TransactionStatementResult> ultimas_transacoes);
public record struct PostTransactionResult(int limite, int saldo);

