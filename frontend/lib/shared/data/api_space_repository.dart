import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/space_repository.dart';
import 'package:weaver/shared/models/space.dart';

/// Not cached, unlike statuses: the list is one small request, and a fresh
/// one on every board load picks up a Space an admin just added or renamed.
@LazySingleton(as: SpaceRepository)
class ApiSpaceRepository implements SpaceRepository {
  ApiSpaceRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  @override
  Future<List<Space>> loadSpaces() => _api.get(
    '/spaces',
    (json) => JsonApiClient.listOf(json, Space.fromJson),
    failureMessage: 'Failed to load Spaces',
  );
}
