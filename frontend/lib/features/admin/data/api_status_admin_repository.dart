import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/admin/data/status_admin_repository.dart';
import 'package:weaver/features/admin/models/admin_status.dart';
import 'package:weaver/features/admin/models/status_details.dart';
import 'package:weaver/shared/data/status_repository.dart';

/// Every successful write drops the shared status cache, so the board and
/// detail dialog pick up the change next time they load.
@LazySingleton(as: StatusAdminRepository)
class ApiStatusAdminRepository implements StatusAdminRepository {
  ApiStatusAdminRepository(
    http.Client client,
    ApiEndpoint endpoint,
    this._statusCache,
  ) : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;
  final StatusRepository _statusCache;

  static AdminStatus _status(Object? json) =>
      AdminStatus.fromJson(json as Map<String, dynamic>);

  static List<AdminStatus> _statuses(Object? json) =>
      JsonApiClient.listOf(json, AdminStatus.fromJson);

  @override
  Future<List<AdminStatus>> list() => _api.get(
    '/statuses',
    _statuses,
    failureMessage: 'Failed to load statuses',
  );

  @override
  Future<AdminStatus> create(StatusDetails details) => _invalidatingCache(
    _api.post(
      '/statuses',
      details.toJson(),
      _status,
      failureMessage: 'Failed to add the status',
    ),
  );

  @override
  Future<AdminStatus> update(String id, StatusDetails details) =>
      _invalidatingCache(
        _api.put(
          '/statuses/$id',
          details.toJson(),
          _status,
          failureMessage: 'Failed to save the status',
        ),
      );

  @override
  Future<List<AdminStatus>> reorder(List<String> orderedIds) =>
      _invalidatingCache(
        _api.put(
          '/statuses/order',
          {'orderedIds': orderedIds},
          _statuses,
          failureMessage: 'Failed to reorder the statuses',
        ),
      );

  @override
  Future<void> delete(String id, {required String replacementId}) =>
      _invalidatingCache(
        _api.delete(
          '/statuses/$id',
          query: {'replacementId': replacementId},
          failureMessage: 'Failed to delete the status',
        ),
      );

  Future<T> _invalidatingCache<T>(Future<T> write) async {
    final result = await write;
    _statusCache.invalidate();
    return result;
  }
}
