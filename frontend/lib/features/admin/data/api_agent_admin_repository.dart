import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/admin/data/agent_admin_repository.dart';
import 'package:weaver/features/admin/models/agent_account.dart';
import 'package:weaver/features/admin/models/api_key.dart';
import 'package:weaver/features/admin/models/api_key_scope.dart';
import 'package:weaver/features/admin/models/issued_api_key.dart';
import 'package:weaver/shared/data/user_directory_repository.dart';

/// Creating or (de)activating an agent drops the shared user directory
/// cache, so assignee pickers pick the change up next time they load. Key
/// changes don't affect the directory.
@LazySingleton(as: AgentAdminRepository)
class ApiAgentAdminRepository implements AgentAdminRepository {
  ApiAgentAdminRepository(
    http.Client client,
    ApiEndpoint endpoint,
    this._directory,
  ) : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;
  final UserDirectoryRepository _directory;

  static AgentAccount _agent(Object? json) =>
      AgentAccount.fromJson(json as Map<String, dynamic>);

  @override
  Future<List<AgentAccount>> list() => _api.get(
    '/admin/agents',
    (json) => JsonApiClient.listOf(json, AgentAccount.fromJson),
    failureMessage: 'Failed to load agents',
  );

  @override
  Future<AgentAccount> create({required String name, String? description}) =>
      _invalidatingDirectory(
        _api.post(
          '/admin/agents',
          {'name': name, 'description': description},
          _agent,
          failureMessage: 'Failed to add the agent',
        ),
      );

  @override
  Future<AgentAccount> setActive(String id, {required bool isActive}) =>
      _invalidatingDirectory(
        _api.put(
          '/admin/agents/$id/active',
          {'isActive': isActive},
          _agent,
          failureMessage: isActive
              ? 'Failed to reactivate the agent'
              : 'Failed to deactivate the agent',
        ),
      );

  @override
  Future<List<ApiKey>> listKeys(String agentId) => _api.get(
    '/admin/agents/$agentId/keys',
    (json) => JsonApiClient.listOf(json, ApiKey.fromJson),
    failureMessage: 'Failed to load the keys',
  );

  @override
  Future<IssuedApiKey> issueKey(
    String agentId, {
    required String label,
    required ApiKeyScope scope,
  }) => _api.post(
    '/admin/agents/$agentId/keys',
    {'label': label, 'scope': scope.wire},
    (json) => IssuedApiKey.fromJson(json as Map<String, dynamic>),
    failureMessage: 'Failed to issue the key',
  );

  @override
  Future<void> revokeKey(String agentId, String keyId) => _api.delete(
    '/admin/agents/$agentId/keys/$keyId',
    failureMessage: 'Failed to revoke the key',
  );

  Future<T> _invalidatingDirectory<T>(Future<T> write) async {
    final result = await write;
    _directory.invalidate();
    return result;
  }
}
