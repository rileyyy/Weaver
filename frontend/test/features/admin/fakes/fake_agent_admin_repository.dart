import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/agent_admin_repository.dart';
import 'package:weaver/features/admin/models/agent_account.dart';
import 'package:weaver/features/admin/models/api_key.dart';
import 'package:weaver/features/admin/models/api_key_scope.dart';
import 'package:weaver/features/admin/models/issued_api_key.dart';

AgentAccount agentAccount(
  String id, {
  String? name,
  String? description,
  String? handle,
  bool isActive = true,
}) => AgentAccount(
  id: id,
  name: name ?? id,
  handle: handle,
  description: description,
  isActive: isActive,
  createdAt: DateTime(2026, 11),
);

ApiKey apiKey(
  String id, {
  String? label,
  ApiKeyScope scope = ApiKeyScope.readOnly,
  DateTime? lastUsedAt,
  DateTime? revokedAt,
}) => ApiKey(
  id: id,
  label: label ?? id,
  scope: scope,
  createdAt: DateTime(2026, 11, 1, 9),
  lastUsedAt: lastUsedAt,
  revokedAt: revokedAt,
);

/// Holds agents and their keys in memory; revoking marks a key revoked and
/// keeps it, as the server does.
class FakeAgentAdminRepository implements AgentAdminRepository {
  FakeAgentAdminRepository(this.agents, {Map<String, List<ApiKey>>? keys})
    : keys = keys ?? {};

  List<AgentAccount> agents;
  final Map<String, List<ApiKey>> keys;
  ApiException? error;
  final List<String> calls = [];

  void _failIfSet() {
    final error = this.error;
    if (error != null) throw error;
  }

  @override
  Future<List<AgentAccount>> list() async {
    _failIfSet();
    return agents;
  }

  @override
  Future<AgentAccount> create({
    required String name,
    String? description,
    String? handle,
  }) async {
    calls.add(
      'create $name ${description ?? '-'}${handle == null ? '' : ' @$handle'}',
    );
    _failIfSet();
    final created = agentAccount(
      'new',
      name: name,
      description: description,
      handle: handle,
    );
    agents = [...agents, created];
    return created;
  }

  @override
  Future<AgentAccount> setHandle(String id, String? handle) async {
    calls.add('setHandle $id ${handle ?? '-'}');
    _failIfSet();
    final agent = agents.firstWhere((agent) => agent.id == id);
    final updated = agentAccount(
      id,
      name: agent.name,
      description: agent.description,
      handle: handle,
      isActive: agent.isActive,
    );
    agents = [for (final a in agents) a.id == id ? updated : a];
    return updated;
  }

  @override
  Future<AgentAccount> setActive(String id, {required bool isActive}) async {
    calls.add('setActive $id $isActive');
    _failIfSet();
    final agent = agents.firstWhere((agent) => agent.id == id);
    return agentAccount(id, name: agent.name, isActive: isActive);
  }

  @override
  Future<List<ApiKey>> listKeys(String agentId) async {
    _failIfSet();
    return keys[agentId] ?? const [];
  }

  @override
  Future<IssuedApiKey> issueKey(
    String agentId, {
    required String label,
    required ApiKeyScope scope,
  }) async {
    calls.add('issueKey $agentId $label ${scope.wire}');
    _failIfSet();
    final issued = apiKey('k-new', label: label, scope: scope);
    keys[agentId] = [...?keys[agentId], issued];
    return IssuedApiKey(apiKey: issued, key: 'wvr_secret-$label');
  }

  @override
  Future<void> revokeKey(String agentId, String keyId) async {
    calls.add('revokeKey $agentId $keyId');
    _failIfSet();
    keys[agentId] = [
      for (final key in keys[agentId] ?? const <ApiKey>[])
        key.id == keyId
            ? apiKey(
                key.id,
                label: key.label,
                scope: key.scope,
                revokedAt: DateTime(2026, 11, 2),
              )
            : key,
    ];
  }
}
