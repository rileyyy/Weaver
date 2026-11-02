import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/work_item_detail/data/work_item_history_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';

@LazySingleton(as: WorkItemHistoryRepository)
class ApiWorkItemHistoryRepository implements WorkItemHistoryRepository {
  ApiWorkItemHistoryRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  @override
  Future<List<WorkItemHistoryEntry>> loadHistory(String workItemId) => _api.get(
    '/work-items/$workItemId/history',
    (json) => JsonApiClient.listOf(json, WorkItemHistoryEntry.fromJson),
    failureMessage: 'Failed to load history',
  );
}
