import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/work_item_search_repository.dart';
import 'package:weaver/shared/models/work_item.dart';

@LazySingleton(as: WorkItemSearchRepository)
class ApiWorkItemSearchRepository implements WorkItemSearchRepository {
  ApiWorkItemSearchRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  /// A picker shows one page and asks you to narrow the search rather than
  /// paging, so this is what fits on screen, not the server's maximum.
  static const int pageSize = 20;

  final JsonApiClient _api;

  @override
  Future<List<WorkItem>> search({String? spaceId, required String text}) {
    final trimmed = text.trim();
    return _api.get(
      '/work-items/search',
      (json) => JsonApiClient.listOf(
        (json as Map<String, dynamic>)['items'],
        WorkItem.fromJson,
      ),
      query: {
        'spaceIds': ?spaceId,
        if (trimmed.isNotEmpty) 'text': trimmed,
        'limit': '$pageSize',
      },
      failureMessage: 'Failed to search work items',
    );
  }
}
