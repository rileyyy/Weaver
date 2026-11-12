import 'package:weaver/features/admin/models/agent_account.dart';
import 'package:weaver/features/admin/models/api_key.dart';
import 'package:weaver/features/admin/models/api_key_scope.dart';
import 'package:weaver/features/admin/models/issued_api_key.dart';

/// Agent accounts and their API keys. Every call needs an admin; the server
/// answers 403 otherwise, and 409 for a human's id.
abstract class AgentAdminRepository {
  Future<List<AgentAccount>> list();

  Future<AgentAccount> create({
    required String name,
    String? description,
    String? handle,
  });

  /// A null [handle] clears it. The server answers 409 if another account
  /// has it.
  Future<AgentAccount> setHandle(String id, String? handle);

  /// Doesn't revoke the agent's keys; they stop working while it's inactive.
  Future<AgentAccount> setActive(String id, {required bool isActive});

  /// Includes revoked keys.
  Future<List<ApiKey>> listKeys(String agentId);

  Future<IssuedApiKey> issueKey(
    String agentId, {
    required String label,
    required ApiKeyScope scope,
  });

  Future<void> revokeKey(String agentId, String keyId);
}
