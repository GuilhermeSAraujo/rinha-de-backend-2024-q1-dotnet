using System.Text.Json.Serialization;

namespace rinha_de_backend_2024_q1_dotnet.Types;

[JsonSerializable(typeof(TransactionRequest))]
[JsonSerializable(typeof(TransactionResult))]
[JsonSerializable(typeof(BalanceStatementResult))]
[JsonSerializable(typeof(TransactionStatementResult))]
[JsonSerializable(typeof(BalanceStatementJson))]
[JsonSerializable(typeof(PostTransactionResult))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}