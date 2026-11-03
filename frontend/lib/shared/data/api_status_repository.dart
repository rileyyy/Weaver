import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/status_repository.dart';
import 'package:weaver/shared/models/work_item_status.dart';

/// Statuses rarely change (only admins edit them), so each Space's are
/// fetched once and shared by the board and the detail dialog until
/// [invalidate] is called or the app is pointed at another server. A failed
/// fetch isn't cached. Another person's edits show up after a reload of the
/// app.
@LazySingleton(as: StatusRepository)
class ApiStatusRepository implements StatusRepository {
  ApiStatusRepository(http.Client client, ApiEndpoint endpoint)
    : _endpoint = endpoint,
      _api = JsonApiClient(client, endpoint);

  final ApiEndpoint _endpoint;
  final JsonApiClient _api;
  final Map<String, Future<List<WorkItemStatus>>> _statusesBySpace = {};
  String? _fetchedFrom;

  @override
  Future<List<WorkItemStatus>> loadStatuses(String spaceId) {
    final baseUrl = _endpoint.baseUrl;
    if (baseUrl != _fetchedFrom) {
      _statusesBySpace.clear();
      _fetchedFrom = baseUrl;
    }

    return _statusesBySpace[spaceId] ??= _api
        .get(
          '/statuses',
          query: {'spaceId': spaceId},
          (json) => JsonApiClient.listOf(json, WorkItemStatus.fromJson),
          failureMessage: 'Failed to load statuses',
        )
        .onError<Object>((error, stackTrace) {
          _statusesBySpace.removeWhere((cached, _) => cached == spaceId);
          Error.throwWithStackTrace(error, stackTrace);
        });
  }

  @override
  void invalidate() => _statusesBySpace.clear();
}
