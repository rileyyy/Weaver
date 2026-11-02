import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/user_directory_repository.dart';
import 'package:weaver/shared/models/user.dart';

/// Accounts only change when an admin changes them, so, like statuses, the
/// list is fetched once and shared by the board and the detail dialog until
/// [invalidate] is called (the Admin page does, after every account change)
/// or the app is pointed at another server. A failed fetch isn't cached.
/// Another admin's changes show up after a reload of the app.
@LazySingleton(as: UserDirectoryRepository)
class ApiUserDirectoryRepository implements UserDirectoryRepository {
  ApiUserDirectoryRepository(http.Client client, ApiEndpoint endpoint)
    : _endpoint = endpoint,
      _api = JsonApiClient(client, endpoint);

  final ApiEndpoint _endpoint;
  final JsonApiClient _api;
  Future<List<User>>? _users;
  String? _fetchedFrom;

  @override
  Future<List<User>> loadUsers() {
    final baseUrl = _endpoint.baseUrl;
    if (baseUrl != _fetchedFrom) {
      _users = null;
      _fetchedFrom = baseUrl;
    }

    return _users ??= _api
        .get(
          '/users',
          (json) => JsonApiClient.listOf(json, User.fromJson),
          failureMessage: 'Failed to load users',
        )
        .onError<Object>((error, stackTrace) {
          _users = null;
          Error.throwWithStackTrace(error, stackTrace);
        });
  }

  @override
  void invalidate() => _users = null;
}
