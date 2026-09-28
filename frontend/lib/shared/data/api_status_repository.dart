import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/status_repository.dart';
import 'package:weaver/shared/models/work_item_status.dart';

/// Statuses rarely change (only admins edit them), so they're fetched once
/// and shared by the board and the detail dialog until [invalidate] is
/// called. A failed fetch isn't cached. Another person's edits show up
/// after a reload of the app.
@LazySingleton(as: StatusRepository)
class ApiStatusRepository implements StatusRepository {
  ApiStatusRepository(http.Client client, @Named('apiBaseUrl') String baseUrl)
    : _api = JsonApiClient(client, baseUrl);

  final JsonApiClient _api;
  Future<List<WorkItemStatus>>? _statuses;

  @override
  Future<List<WorkItemStatus>> loadStatuses() => _statuses ??= _api
      .get(
        '/statuses',
        (json) => JsonApiClient.listOf(json, WorkItemStatus.fromJson),
        failureMessage: 'Failed to load statuses',
      )
      .onError<Object>((error, stackTrace) {
        _statuses = null;
        Error.throwWithStackTrace(error, stackTrace);
      });

  @override
  void invalidate() => _statuses = null;
}
