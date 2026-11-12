import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/admin/data/run_report_repository.dart';
import 'package:weaver/features/admin/models/run_changed_item.dart';
import 'package:weaver/features/admin/models/run_report.dart';
import 'package:weaver/features/admin/models/run_report_page.dart';
import 'package:weaver/features/admin/models/run_undo_result.dart';

@LazySingleton(as: RunReportRepository)
class ApiRunReportRepository implements RunReportRepository {
  ApiRunReportRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  @override
  Future<RunReportPage> list({required int offset}) => _api.get(
    '/admin/runs',
    (json) => RunReportPage.fromJson(json as Map<String, dynamic>),
    query: {'offset': '$offset'},
    failureMessage: 'Failed to load the runs',
  );

  @override
  Future<List<RunChangedItem>> changes(RunReport run) => _api.get(
    '/admin/runs/changes',
    (json) => JsonApiClient.listOf(json, RunChangedItem.fromJson),
    query: _runQuery(run),
    failureMessage: 'Failed to load the run’s changes',
  );

  @override
  Future<RunUndoResult> undo(RunReport run) => _api.post(
    '/admin/runs/undo',
    null,
    (json) => RunUndoResult.fromJson(json as Map<String, dynamic>),
    query: _runQuery(run),
    failureMessage: 'Failed to undo the run',
  );

  /// The run id is free text, so it goes in the query, not the path.
  static Map<String, Object> _runQuery(RunReport run) => {
    'accountId': run.accountId,
    'apiKeyId': run.apiKeyId,
    'runId': run.runId,
  };
}
