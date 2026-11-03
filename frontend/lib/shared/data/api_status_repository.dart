import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/space_lookup_cache.dart';
import 'package:weaver/shared/data/status_repository.dart';
import 'package:weaver/shared/models/work_item_status.dart';

/// Shared by the board and the detail dialog, cached per Space (see
/// [SpaceLookupCache]). Another person's edits show up after a reload of
/// the app.
@LazySingleton(as: StatusRepository)
class ApiStatusRepository implements StatusRepository {
  ApiStatusRepository(http.Client client, ApiEndpoint endpoint)
    : _cache = SpaceLookupCache(
        endpoint,
        (spaceId) => JsonApiClient(client, endpoint).get(
          '/statuses',
          (json) => JsonApiClient.listOf(json, WorkItemStatus.fromJson),
          query: {'spaceId': spaceId},
          failureMessage: 'Failed to load statuses',
        ),
      );

  final SpaceLookupCache<WorkItemStatus> _cache;

  @override
  Future<List<WorkItemStatus>> loadStatuses(String spaceId) =>
      _cache.load(spaceId);

  @override
  void invalidate() => _cache.invalidate();
}
