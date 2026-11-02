import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/admin/data/agent_admin_repository.dart';
import 'package:weaver/features/admin/models/api_key.dart';
import 'package:weaver/features/admin/models/api_key_scope.dart';
import 'package:weaver/features/admin/state/admin_error_message.dart';

/// Either the new key's plaintext, to show once, or why it wasn't issued.
typedef KeyIssueResult = ({String? key, String? error});

/// One agent's keys, on the page the Agents tab opens for it. Revoked keys
/// stay listed, as the server keeps them.
@injectable
class AgentKeysViewModel extends ViewModel {
  AgentKeysViewModel(this._repository, @factoryParam this.agentId);

  final AgentAdminRepository _repository;
  final String agentId;

  List<ApiKey> _keys = const [];
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;
  int _loadGeneration = 0;

  List<ApiKey> get keys => _keys;

  /// Only true for the first load; later refreshes keep showing the list.
  bool get isLoading => _isLoading && !_hasLoaded;
  String? get loadError => _loadError;

  Future<void> load() async {
    final generation = ++_loadGeneration;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      final keys = await _repository.listKeys(agentId);
      if (generation != _loadGeneration) return;
      _keys = keys;
      _hasLoaded = true;
    } on ApiException {
      if (generation != _loadGeneration) return;
      _loadError = 'Could not load the keys. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  Future<KeyIssueResult> issueKey({
    required String label,
    required ApiKeyScope scope,
  }) async {
    final trimmedLabel = label.trim();
    if (trimmedLabel.isEmpty) return (key: null, error: 'Enter a label.');
    try {
      final issued = await _repository.issueKey(
        agentId,
        label: trimmedLabel,
        scope: scope,
      );
      _keys = [..._keys, issued.apiKey];
      notifyIfActive();
      return (key: issued.key, error: null);
    } on ApiException catch (e) {
      return (
        key: null,
        error: adminErrorMessage(e, 'Could not issue the key. Try again.'),
      );
    }
  }

  /// Reloads afterwards rather than guessing the revocation time, since the
  /// server answers with no body.
  Future<String?> revokeKey(String keyId) async {
    try {
      await _repository.revokeKey(agentId, keyId);
    } on ApiException catch (e) {
      return adminErrorMessage(e, 'Could not revoke the key. Try again.');
    }
    if (!isDisposed) await load();
    return null;
  }
}
