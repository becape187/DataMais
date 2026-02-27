
root@datamais:/home/becape# sudo journalctl -u datamais.service -f
Feb 27 17:20:20 datamais datamais[28933]: ✓ Variáveis de ambiente já carregadas pelo systemd (EnvironmentFile)
Feb 27 17:20:20 datamais datamais[28933]: ✓ POSTGRES_PASSWORD carregada com sucesso (14 caracteres)
Feb 27 17:20:20 datamais datamais[28933]: info: Microsoft.Hosting.Lifetime[14]
Feb 27 17:20:20 datamais datamais[28933]:       Now listening on: http://0.0.0.0:5000
Feb 27 17:20:20 datamais datamais[28933]: info: Microsoft.Hosting.Lifetime[0]
Feb 27 17:20:20 datamais datamais[28933]:       Application started. Press Ctrl+C to shut down.
Feb 27 17:20:20 datamais datamais[28933]: info: Microsoft.Hosting.Lifetime[0]
Feb 27 17:20:20 datamais datamais[28933]:       Hosting environment: Production
Feb 27 17:20:20 datamais datamais[28933]: info: Microsoft.Hosting.Lifetime[0]
Feb 27 17:20:20 datamais datamais[28933]:       Content root path: /home/becape/datamais.api
Feb 27 17:20:27 datamais datamais[28933]: fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
Feb 27 17:20:27 datamais datamais[28933]:       Failed executing DbCommand (3ms) [Parameters=[@__p_0='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
Feb 27 17:20:27 datamais datamais[28933]:       SELECT t."Id", t."CaminhoArquivo", t."CilindroId", t."ClienteId", t."Data", t."DataAtualizacao", t."DataCriacao", t."EnsaioId", t."Numero", t."Observacoes", c."Id", c."Cnpj", c."Contato", c."DataAtualizacao", c."DataCriacao", c."Email", c."Nome", c0."Id", c0."CargaNominalA", c0."CargaNominalB", c0."ClienteId", c0."CodigoCliente", c0."CodigoInterno", c0."ComprimentoHaste", c0."DataAtualizacao", c0."DataCriacao", c0."DataFabricacao", c0."Descricao", c0."DiametroHaste", c0."DiametroInterno", c0."Fabricante", c0."HistereseAlarmeA", c0."HistereseAlarmeB", c0."MaximaPressaoSegurancaA", c0."MaximaPressaoSegurancaB", c0."MaximaPressaoSuportadaA", c0."MaximaPressaoSuportadaB", c0."Modelo", c0."Nome", c0."PercentualVariacaoAlarmeA", c0."PercentualVariacaoAlarmeB", c0."PercentualVariacaoDesligaProcessoA", c0."PercentualVariacaoDesligaProcessoB", c0."PreCargaA", c0."PreCargaB", c0."TempoDuracaoCargaA", c0."TempoDuracaoCargaB", c0."TempoRampaDescidaA", c0."TempoRampaDescidaB", c0."TempoRampaSubidaA", c0."TempoRampaSubidaB", e."Id", e."CamaraTestada", e."CilindroId", e."ClienteId", e."DataAtualizacao", e."DataCriacao", e."DataFim", e."DataInicio", e."Numero", e."Observacoes", e."PressaoCargaConfigurada", e."Status", e."TempoCargaConfigurado"
Feb 27 17:20:27 datamais datamais[28933]:       FROM (
Feb 27 17:20:27 datamais datamais[28933]:           SELECT r."Id", r."CaminhoArquivo", r."CilindroId", r."ClienteId", r."Data", r."DataAtualizacao", r."DataCriacao", r."EnsaioId", r."Numero", r."Observacoes"
Feb 27 17:20:27 datamais datamais[28933]:           FROM "Relatorios" AS r
Feb 27 17:20:27 datamais datamais[28933]:           ORDER BY r."Data" DESC
Feb 27 17:20:27 datamais datamais[28933]:           LIMIT @__p_0
Feb 27 17:20:27 datamais datamais[28933]:       ) AS t
Feb 27 17:20:27 datamais datamais[28933]:       INNER JOIN "Clientes" AS c ON t."ClienteId" = c."Id"
Feb 27 17:20:27 datamais datamais[28933]:       INNER JOIN "Cilindros" AS c0 ON t."CilindroId" = c0."Id"
Feb 27 17:20:27 datamais datamais[28933]:       LEFT JOIN "Ensaios" AS e ON t."EnsaioId" = e."Id"
Feb 27 17:20:27 datamais datamais[28933]:       ORDER BY t."Data" DESC
Feb 27 17:20:27 datamais datamais[28933]: fail: Microsoft.EntityFrameworkCore.Query[10100]
Feb 27 17:20:27 datamais datamais[28933]:       An exception occurred while iterating over the results of a query for context type 'DataMais.Data.DataMaisDbContext'.
Feb 27 17:20:27 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:27 datamais datamais[28933]:
Feb 27 17:20:27 datamais datamais[28933]:       POSITION: 1036
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:27 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:27 datamais datamais[28933]:         Exception data:
Feb 27 17:20:27 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:27 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:27 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:27 datamais datamais[28933]:           Position: 1036
Feb 27 17:20:27 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:27 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:27 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:27 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:27 datamais datamais[28933]:
Feb 27 17:20:27 datamais datamais[28933]:       POSITION: 1036
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:27 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:27 datamais datamais[28933]:         Exception data:
Feb 27 17:20:27 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:27 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:27 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:27 datamais datamais[28933]:           Position: 1036
Feb 27 17:20:27 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:27 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:27 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:27 datamais datamais[28933]: fail: DataMais.Controllers.RelatorioController[0]
Feb 27 17:20:27 datamais datamais[28933]:       Erro ao listar últimos relatórios
Feb 27 17:20:27 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:27 datamais datamais[28933]:
Feb 27 17:20:27 datamais datamais[28933]:       POSITION: 1036
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:27 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
Feb 27 17:20:27 datamais datamais[28933]:          at DataMais.Controllers.RelatorioController.GetUltimos(Int32 top) in /home/runner/work/DataMais/DataMais/DataMais/Controllers/RelatorioController.cs:line 75
Feb 27 17:20:27 datamais datamais[28933]:         Exception data:
Feb 27 17:20:27 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:27 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:27 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:27 datamais datamais[28933]:           Position: 1036
Feb 27 17:20:27 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:27 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:27 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:39 datamais datamais[28933]: fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
Feb 27 17:20:39 datamais datamais[28933]:       Failed executing DbCommand (1ms) [Parameters=[@__p_0='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
Feb 27 17:20:39 datamais datamais[28933]:       SELECT t."Id", t."CaminhoArquivo", t."CilindroId", t."ClienteId", t."Data", t."DataAtualizacao", t."DataCriacao", t."EnsaioId", t."Numero", t."Observacoes", c."Id", c."Cnpj", c."Contato", c."DataAtualizacao", c."DataCriacao", c."Email", c."Nome", c0."Id", c0."CargaNominalA", c0."CargaNominalB", c0."ClienteId", c0."CodigoCliente", c0."CodigoInterno", c0."ComprimentoHaste", c0."DataAtualizacao", c0."DataCriacao", c0."DataFabricacao", c0."Descricao", c0."DiametroHaste", c0."DiametroInterno", c0."Fabricante", c0."HistereseAlarmeA", c0."HistereseAlarmeB", c0."MaximaPressaoSegurancaA", c0."MaximaPressaoSegurancaB", c0."MaximaPressaoSuportadaA", c0."MaximaPressaoSuportadaB", c0."Modelo", c0."Nome", c0."PercentualVariacaoAlarmeA", c0."PercentualVariacaoAlarmeB", c0."PercentualVariacaoDesligaProcessoA", c0."PercentualVariacaoDesligaProcessoB", c0."PreCargaA", c0."PreCargaB", c0."TempoDuracaoCargaA", c0."TempoDuracaoCargaB", c0."TempoRampaDescidaA", c0."TempoRampaDescidaB", c0."TempoRampaSubidaA", c0."TempoRampaSubidaB", e."Id", e."CamaraTestada", e."CilindroId", e."ClienteId", e."DataAtualizacao", e."DataCriacao", e."DataFim", e."DataInicio", e."Numero", e."Observacoes", e."PressaoCargaConfigurada", e."Status", e."TempoCargaConfigurado"
Feb 27 17:20:39 datamais datamais[28933]:       FROM (
Feb 27 17:20:39 datamais datamais[28933]:           SELECT r."Id", r."CaminhoArquivo", r."CilindroId", r."ClienteId", r."Data", r."DataAtualizacao", r."DataCriacao", r."EnsaioId", r."Numero", r."Observacoes"
Feb 27 17:20:39 datamais datamais[28933]:           FROM "Relatorios" AS r
Feb 27 17:20:39 datamais datamais[28933]:           ORDER BY r."Data" DESC
Feb 27 17:20:39 datamais datamais[28933]:           LIMIT @__p_0
Feb 27 17:20:39 datamais datamais[28933]:       ) AS t
Feb 27 17:20:39 datamais datamais[28933]:       INNER JOIN "Clientes" AS c ON t."ClienteId" = c."Id"
Feb 27 17:20:39 datamais datamais[28933]:       INNER JOIN "Cilindros" AS c0 ON t."CilindroId" = c0."Id"
Feb 27 17:20:39 datamais datamais[28933]:       LEFT JOIN "Ensaios" AS e ON t."EnsaioId" = e."Id"
Feb 27 17:20:39 datamais datamais[28933]:       ORDER BY t."Data" DESC
Feb 27 17:20:39 datamais datamais[28933]: fail: Microsoft.EntityFrameworkCore.Query[10100]
Feb 27 17:20:39 datamais datamais[28933]:       An exception occurred while iterating over the results of a query for context type 'DataMais.Data.DataMaisDbContext'.
Feb 27 17:20:39 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:39 datamais datamais[28933]:
Feb 27 17:20:39 datamais datamais[28933]:       POSITION: 1036
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:39 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:39 datamais datamais[28933]:         Exception data:
Feb 27 17:20:39 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:39 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:39 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:39 datamais datamais[28933]:           Position: 1036
Feb 27 17:20:39 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:39 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:39 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:39 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:39 datamais datamais[28933]:
Feb 27 17:20:39 datamais datamais[28933]:       POSITION: 1036
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:39 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:39 datamais datamais[28933]:         Exception data:
Feb 27 17:20:39 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:39 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:39 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:39 datamais datamais[28933]:           Position: 1036
Feb 27 17:20:39 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:39 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:39 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:39 datamais datamais[28933]: fail: DataMais.Controllers.RelatorioController[0]
Feb 27 17:20:39 datamais datamais[28933]:       Erro ao listar últimos relatórios
Feb 27 17:20:39 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:39 datamais datamais[28933]:
Feb 27 17:20:39 datamais datamais[28933]:       POSITION: 1036
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:39 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
Feb 27 17:20:39 datamais datamais[28933]:          at DataMais.Controllers.RelatorioController.GetUltimos(Int32 top) in /home/runner/work/DataMais/DataMais/DataMais/Controllers/RelatorioController.cs:line 75
Feb 27 17:20:39 datamais datamais[28933]:         Exception data:
Feb 27 17:20:39 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:39 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:39 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:39 datamais datamais[28933]:           Position: 1036
Feb 27 17:20:39 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:39 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:39 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:45 datamais datamais[28933]: ⚠️ Aviso: Não foi possível salvar no arquivo /home/becape/datamais.env (sem permissão). Configuração atualizada apenas em memória.
Feb 27 17:20:46 datamais datamais[28933]: warn: Microsoft.EntityFrameworkCore.Query[20504]
Feb 27 17:20:46 datamais datamais[28933]:       Compiling a query which loads related collections for more than one collection navigation, either via 'Include' or through projection, but no 'QuerySplittingBehavior' has been configured. By default, Entity Framework will use 'QuerySplittingBehavior.SingleQuery', which can potentially result in slow query performance. See https://go.microsoft.com/fwlink/?linkid=2134277 for more information. To identify the query that's triggering this warning call 'ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning))'.
Feb 27 17:20:46 datamais datamais[28933]: fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
Feb 27 17:20:46 datamais datamais[28933]:       Failed executing DbCommand (3ms) [Parameters=[@__id_0='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
Feb 27 17:20:46 datamais datamais[28933]:       SELECT t."Id", t."Cnpj", t."Contato", t."DataAtualizacao", t."DataCriacao", t."Email", t."Nome", c0."Id", c0."CargaNominalA", c0."CargaNominalB", c0."ClienteId", c0."CodigoCliente", c0."CodigoInterno", c0."ComprimentoHaste", c0."DataAtualizacao", c0."DataCriacao", c0."DataFabricacao", c0."Descricao", c0."DiametroHaste", c0."DiametroInterno", c0."Fabricante", c0."HistereseAlarmeA", c0."HistereseAlarmeB", c0."MaximaPressaoSegurancaA", c0."MaximaPressaoSegurancaB", c0."MaximaPressaoSuportadaA", c0."MaximaPressaoSuportadaB", c0."Modelo", c0."Nome", c0."PercentualVariacaoAlarmeA", c0."PercentualVariacaoAlarmeB", c0."PercentualVariacaoDesligaProcessoA", c0."PercentualVariacaoDesligaProcessoB", c0."PreCargaA", c0."PreCargaB", c0."TempoDuracaoCargaA", c0."TempoDuracaoCargaB", c0."TempoRampaDescidaA", c0."TempoRampaDescidaB", c0."TempoRampaSubidaA", c0."TempoRampaSubidaB", e."Id", e."CamaraTestada", e."CilindroId", e."ClienteId", e."DataAtualizacao", e."DataCriacao", e."DataFim", e."DataInicio", e."Numero", e."Observacoes", e."PressaoCargaConfigurada", e."Status", e."TempoCargaConfigurado"
Feb 27 17:20:46 datamais datamais[28933]:       FROM (
Feb 27 17:20:46 datamais datamais[28933]:           SELECT c."Id", c."Cnpj", c."Contato", c."DataAtualizacao", c."DataCriacao", c."Email", c."Nome"
Feb 27 17:20:46 datamais datamais[28933]:           FROM "Clientes" AS c
Feb 27 17:20:46 datamais datamais[28933]:           WHERE c."Id" = @__id_0
Feb 27 17:20:46 datamais datamais[28933]:           LIMIT 1
Feb 27 17:20:46 datamais datamais[28933]:       ) AS t
Feb 27 17:20:46 datamais datamais[28933]:       LEFT JOIN "Cilindros" AS c0 ON t."Id" = c0."ClienteId"
Feb 27 17:20:46 datamais datamais[28933]:       LEFT JOIN "Ensaios" AS e ON t."Id" = e."ClienteId"
Feb 27 17:20:46 datamais datamais[28933]:       ORDER BY t."Id", c0."Id"
Feb 27 17:20:46 datamais datamais[28933]: fail: Microsoft.EntityFrameworkCore.Query[10100]
Feb 27 17:20:46 datamais datamais[28933]:       An exception occurred while iterating over the results of a query for context type 'DataMais.Data.DataMaisDbContext'.
Feb 27 17:20:46 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:
Feb 27 17:20:46 datamais datamais[28933]:       POSITION: 886
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:46 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:46 datamais datamais[28933]:         Exception data:
Feb 27 17:20:46 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:46 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:46 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:           Position: 886
Feb 27 17:20:46 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:46 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:46 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:46 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:
Feb 27 17:20:46 datamais datamais[28933]:       POSITION: 886
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:46 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:46 datamais datamais[28933]:         Exception data:
Feb 27 17:20:46 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:46 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:46 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:           Position: 886
Feb 27 17:20:46 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:46 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:46 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:46 datamais datamais[28933]: fail: DataMais.Controllers.ClienteController[0]
Feb 27 17:20:46 datamais datamais[28933]:       Erro ao obter cliente
Feb 27 17:20:46 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:
Feb 27 17:20:46 datamais datamais[28933]:       POSITION: 886
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:46 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.ShapedQueryCompilingExpressionVisitor.SingleOrDefaultAsync[TSource](IAsyncEnumerable`1 asyncEnumerable, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.ShapedQueryCompilingExpressionVisitor.SingleOrDefaultAsync[TSource](IAsyncEnumerable`1 asyncEnumerable, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at DataMais.Controllers.ClienteController.GetById(Int32 id) in /home/runner/work/DataMais/DataMais/DataMais/Controllers/ClienteController.cs:line 52
Feb 27 17:20:46 datamais datamais[28933]:         Exception data:
Feb 27 17:20:46 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:46 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:46 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:           Position: 886
Feb 27 17:20:46 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:46 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:46 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:46 datamais datamais[28933]: fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
Feb 27 17:20:46 datamais datamais[28933]:       Failed executing DbCommand (2ms) [Parameters=[@__p_0='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
Feb 27 17:20:46 datamais datamais[28933]:       SELECT t."Id", t."CaminhoArquivo", t."CilindroId", t."ClienteId", t."Data", t."DataAtualizacao", t."DataCriacao", t."EnsaioId", t."Numero", t."Observacoes", c."Id", c."Cnpj", c."Contato", c."DataAtualizacao", c."DataCriacao", c."Email", c."Nome", c0."Id", c0."CargaNominalA", c0."CargaNominalB", c0."ClienteId", c0."CodigoCliente", c0."CodigoInterno", c0."ComprimentoHaste", c0."DataAtualizacao", c0."DataCriacao", c0."DataFabricacao", c0."Descricao", c0."DiametroHaste", c0."DiametroInterno", c0."Fabricante", c0."HistereseAlarmeA", c0."HistereseAlarmeB", c0."MaximaPressaoSegurancaA", c0."MaximaPressaoSegurancaB", c0."MaximaPressaoSuportadaA", c0."MaximaPressaoSuportadaB", c0."Modelo", c0."Nome", c0."PercentualVariacaoAlarmeA", c0."PercentualVariacaoAlarmeB", c0."PercentualVariacaoDesligaProcessoA", c0."PercentualVariacaoDesligaProcessoB", c0."PreCargaA", c0."PreCargaB", c0."TempoDuracaoCargaA", c0."TempoDuracaoCargaB", c0."TempoRampaDescidaA", c0."TempoRampaDescidaB", c0."TempoRampaSubidaA", c0."TempoRampaSubidaB", e."Id", e."CamaraTestada", e."CilindroId", e."ClienteId", e."DataAtualizacao", e."DataCriacao", e."DataFim", e."DataInicio", e."Numero", e."Observacoes", e."PressaoCargaConfigurada", e."Status", e."TempoCargaConfigurado"
Feb 27 17:20:46 datamais datamais[28933]:       FROM (
Feb 27 17:20:46 datamais datamais[28933]:           SELECT r."Id", r."CaminhoArquivo", r."CilindroId", r."ClienteId", r."Data", r."DataAtualizacao", r."DataCriacao", r."EnsaioId", r."Numero", r."Observacoes"
Feb 27 17:20:46 datamais datamais[28933]:           FROM "Relatorios" AS r
Feb 27 17:20:46 datamais datamais[28933]:           ORDER BY r."Data" DESC
Feb 27 17:20:46 datamais datamais[28933]:           LIMIT @__p_0
Feb 27 17:20:46 datamais datamais[28933]:       ) AS t
Feb 27 17:20:46 datamais datamais[28933]:       INNER JOIN "Clientes" AS c ON t."ClienteId" = c."Id"
Feb 27 17:20:46 datamais datamais[28933]:       INNER JOIN "Cilindros" AS c0 ON t."CilindroId" = c0."Id"
Feb 27 17:20:46 datamais datamais[28933]:       LEFT JOIN "Ensaios" AS e ON t."EnsaioId" = e."Id"
Feb 27 17:20:46 datamais datamais[28933]:       ORDER BY t."Data" DESC
Feb 27 17:20:46 datamais datamais[28933]: fail: Microsoft.EntityFrameworkCore.Query[10100]
Feb 27 17:20:46 datamais datamais[28933]:       An exception occurred while iterating over the results of a query for context type 'DataMais.Data.DataMaisDbContext'.
Feb 27 17:20:46 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:
Feb 27 17:20:46 datamais datamais[28933]:       POSITION: 1036
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:46 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:46 datamais datamais[28933]:         Exception data:
Feb 27 17:20:46 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:46 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:46 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:           Position: 1036
Feb 27 17:20:46 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:46 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:46 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:46 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:
Feb 27 17:20:46 datamais datamais[28933]:       POSITION: 1036
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:46 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:46 datamais datamais[28933]:         Exception data:
Feb 27 17:20:46 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:46 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:46 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:           Position: 1036
Feb 27 17:20:46 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:46 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:46 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:20:46 datamais datamais[28933]: fail: DataMais.Controllers.RelatorioController[0]
Feb 27 17:20:46 datamais datamais[28933]:       Erro ao listar últimos relatórios
Feb 27 17:20:46 datamais datamais[28933]:       Npgsql.PostgresException (0x80004005): 42703: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:
Feb 27 17:20:46 datamais datamais[28933]:       POSITION: 1036
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:20:46 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
Feb 27 17:20:46 datamais datamais[28933]:          at DataMais.Controllers.RelatorioController.GetUltimos(Int32 top) in /home/runner/work/DataMais/DataMais/DataMais/Controllers/RelatorioController.cs:line 75
Feb 27 17:20:46 datamais datamais[28933]:         Exception data:
Feb 27 17:20:46 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:20:46 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:20:46 datamais datamais[28933]:           MessageText: column e.CamaraTestada does not exist
Feb 27 17:20:46 datamais datamais[28933]:           Position: 1036
Feb 27 17:20:46 datamais datamais[28933]:           File: parse_relation.c
Feb 27 17:20:46 datamais datamais[28933]:           Line: 3722
Feb 27 17:20:46 datamais datamais[28933]:           Routine: errorMissingColumn
Feb 27 17:21:07 datamais datamais[28933]: fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
Feb 27 17:21:07 datamais datamais[28933]:       Failed executing DbCommand (3ms) [Parameters=[@p0='?', @p1='?' (DbType = Int32), @p2='?' (DbType = Int32), @p3='?' (DbType = DateTime), @p4='?' (DbType = DateTime), @p5='?' (DbType = DateTime), @p6='?' (DbType = DateTime), @p7='?', @p8='?', @p9='?' (DbType = Decimal), @p10='?', @p11='?' (DbType = Decimal)], CommandType='Text', CommandTimeout='30']
Feb 27 17:21:07 datamais datamais[28933]:       INSERT INTO "Ensaios" ("CamaraTestada", "CilindroId", "ClienteId", "DataAtualizacao", "DataCriacao", "DataFim", "DataInicio", "Numero", "Observacoes", "PressaoCargaConfigurada", "Status", "TempoCargaConfigurado")
Feb 27 17:21:07 datamais datamais[28933]:       VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11)
Feb 27 17:21:07 datamais datamais[28933]:       RETURNING "Id";
Feb 27 17:21:07 datamais datamais[28933]: fail: Microsoft.EntityFrameworkCore.Update[10000]
Feb 27 17:21:07 datamais datamais[28933]:       An exception occurred in the database while saving changes for context type 'DataMais.Data.DataMaisDbContext'.
Feb 27 17:21:07 datamais datamais[28933]:       Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
Feb 27 17:21:07 datamais datamais[28933]:        ---> Npgsql.PostgresException (0x80004005): 42703: column "CamaraTestada" of relation "Ensaios" does not exist
Feb 27 17:21:07 datamais datamais[28933]:
Feb 27 17:21:07 datamais datamais[28933]:       POSITION: 24
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:21:07 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync(IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:         Exception data:
Feb 27 17:21:07 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:21:07 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:21:07 datamais datamais[28933]:           MessageText: column "CamaraTestada" of relation "Ensaios" does not exist
Feb 27 17:21:07 datamais datamais[28933]:           Position: 24
Feb 27 17:21:07 datamais datamais[28933]:           File: parse_target.c
Feb 27 17:21:07 datamais datamais[28933]:           Line: 1066
Feb 27 17:21:07 datamais datamais[28933]:           Routine: checkInsertTargets
Feb 27 17:21:07 datamais datamais[28933]:          --- End of inner exception stack trace ---
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync(IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChangesAsync(IList`1 entriesToSave, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChangesAsync(StateManager stateManager, Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:       Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
Feb 27 17:21:07 datamais datamais[28933]:        ---> Npgsql.PostgresException (0x80004005): 42703: column "CamaraTestada" of relation "Ensaios" does not exist
Feb 27 17:21:07 datamais datamais[28933]:
Feb 27 17:21:07 datamais datamais[28933]:       POSITION: 24
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:21:07 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync(IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:         Exception data:
Feb 27 17:21:07 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:21:07 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:21:07 datamais datamais[28933]:           MessageText: column "CamaraTestada" of relation "Ensaios" does not exist
Feb 27 17:21:07 datamais datamais[28933]:           Position: 24
Feb 27 17:21:07 datamais datamais[28933]:           File: parse_target.c
Feb 27 17:21:07 datamais datamais[28933]:           Line: 1066
Feb 27 17:21:07 datamais datamais[28933]:           Routine: checkInsertTargets
Feb 27 17:21:07 datamais datamais[28933]:          --- End of inner exception stack trace ---
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync(IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChangesAsync(IList`1 entriesToSave, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChangesAsync(StateManager stateManager, Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]: fail: DataMais.Controllers.EnsaioController[0]
Feb 27 17:21:07 datamais datamais[28933]:       Erro ao iniciar ensaio
Feb 27 17:21:07 datamais datamais[28933]:       Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
Feb 27 17:21:07 datamais datamais[28933]:        ---> Npgsql.PostgresException (0x80004005): 42703: column "CamaraTestada" of relation "Ensaios" does not exist
Feb 27 17:21:07 datamais datamais[28933]:
Feb 27 17:21:07 datamais datamais[28933]:       POSITION: 24
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
Feb 27 17:21:07 datamais datamais[28933]:          at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync(IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:         Exception data:
Feb 27 17:21:07 datamais datamais[28933]:           Severity: ERROR
Feb 27 17:21:07 datamais datamais[28933]:           SqlState: 42703
Feb 27 17:21:07 datamais datamais[28933]:           MessageText: column "CamaraTestada" of relation "Ensaios" does not exist
Feb 27 17:21:07 datamais datamais[28933]:           Position: 24
Feb 27 17:21:07 datamais datamais[28933]:           File: parse_target.c
Feb 27 17:21:07 datamais datamais[28933]:           Line: 1066
Feb 27 17:21:07 datamais datamais[28933]:           Routine: checkInsertTargets
Feb 27 17:21:07 datamais datamais[28933]:          --- End of inner exception stack trace ---
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync(IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChangesAsync(IList`1 entriesToSave, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChangesAsync(StateManager stateManager, Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
Feb 27 17:21:07 datamais datamais[28933]:          at DataMais.Controllers.EnsaioController.IniciarEnsaio(IniciarEnsaioRequest request) in /home/runner/work/DataMais/DataMais/DataMais/Controllers/EnsaioController.cs:line 100

