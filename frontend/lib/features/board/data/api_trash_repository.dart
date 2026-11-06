import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/board/data/trash_repository.dart';
import 'package:weaver/features/board/models/trashed_work_item.dart';

@LazySingleton(as: TrashRepository)
class ApiTrashRepository implements TrashRepository {
  ApiTrashRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  @override
  Future<List<TrashedWorkItem>> list() => _api.get(
    '/trash',
    (json) => JsonApiClient.listOf(json, TrashedWorkItem.fromJson),
    failureMessage: 'Failed to load the Trash',
  );

  @override
  Future<void> restore(String workItemId) => _api.postIgnoringBody(
    '/trash/$workItemId/restore',
    null,
    failureMessage: 'Failed to restore the work item',
  );

  @override
  Future<void> purge(String workItemId) => _api.delete(
    '/trash/$workItemId',
    failureMessage: 'Failed to delete the work item permanently',
  );

  @override
  Future<void> empty() =>
      _api.delete('/trash', failureMessage: 'Failed to empty the Trash');
}
