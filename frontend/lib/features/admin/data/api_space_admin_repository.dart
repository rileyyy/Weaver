import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/admin/data/space_admin_repository.dart';
import 'package:weaver/shared/models/space.dart';

@LazySingleton(as: SpaceAdminRepository)
class ApiSpaceAdminRepository implements SpaceAdminRepository {
  ApiSpaceAdminRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  static Space _space(Object? json) =>
      Space.fromJson(json as Map<String, dynamic>);

  @override
  Future<List<Space>> list() => _api.get(
    '/spaces',
    (json) => JsonApiClient.listOf(json, Space.fromJson),
    failureMessage: 'Failed to load Spaces',
  );

  @override
  Future<Space> create(String name) => _api.post(
    '/spaces',
    {'name': name},
    _space,
    failureMessage: 'Failed to add the Space',
  );

  @override
  Future<Space> rename(String id, String name) => _api.put(
    '/spaces/$id',
    {'name': name},
    _space,
    failureMessage: 'Failed to rename the Space',
  );

  @override
  Future<Space> setDoneHidingPeriod(String id, int days) => _api.put(
    '/spaces/$id/done-hiding-period',
    {'days': days},
    _space,
    failureMessage: 'Failed to change the done-hiding period',
  );
}
