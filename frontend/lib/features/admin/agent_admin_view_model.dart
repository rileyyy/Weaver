import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/admin/data/agent_admin_repository.dart';
import 'package:weaver/features/admin/models/agent_account.dart';
import 'package:weaver/features/admin/state/admin_error_message.dart';
import 'package:weaver/shared/models/handle.dart';

/// The Admin page's Agents tab. Each change returns null on success, or why
/// it failed for the dialog or snackbar that asked for it.
@injectable
class AgentAdminViewModel extends ViewModel {
  AgentAdminViewModel(this._repository);

  final AgentAdminRepository _repository;

  List<AgentAccount> _agents = const [];
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;
  int _loadGeneration = 0;

  List<AgentAccount> get agents => _agents;

  /// Only true for the first load; later refreshes keep showing the list.
  bool get isLoading => _isLoading && !_hasLoaded;
  String? get loadError => _loadError;

  Future<void> load() async {
    final generation = ++_loadGeneration;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      final agents = await _repository.list();
      if (generation != _loadGeneration) return;
      _agents = agents;
      _hasLoaded = true;
    } on ApiException {
      if (generation != _loadGeneration) return;
      _loadError = 'Could not load the agents. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  Future<String?> create({
    required String name,
    required String description,
    String handle = '',
  }) {
    final trimmedName = name.trim();
    if (trimmedName.isEmpty) return Future.value('Enter a name.');
    final trimmedHandle = _blankAsNull(handle);
    final problem = trimmedHandle == null ? null : handleProblem(trimmedHandle);
    if (problem != null) return Future.value(problem);
    return _attempt(
      () => _repository.create(
        name: trimmedName,
        description: _blankAsNull(description),
        handle: trimmedHandle,
      ),
      onSuccess: (created) => _agents = _sorted([..._agents, created]),
      fallbackError: 'Could not add the agent. Try again.',
    );
  }

  /// A blank [handle] clears it, so the agent can no longer be mentioned.
  Future<String?> setHandle(String id, String handle) {
    final trimmed = _blankAsNull(handle);
    final problem = trimmed == null ? null : handleProblem(trimmed);
    if (problem != null) return Future.value(problem);
    return _attempt(
      () => _repository.setHandle(id, trimmed),
      onSuccess: _replace,
      fallbackError: 'Could not set the handle. Try again.',
    );
  }

  Future<String?> setActive(String id, {required bool isActive}) => _attempt(
    () => _repository.setActive(id, isActive: isActive),
    onSuccess: _replace,
    fallbackError: isActive
        ? 'Could not reactivate the agent. Try again.'
        : 'Could not deactivate the agent. Try again.',
  );

  Future<String?> _attempt<R>(
    Future<R> Function() change, {
    required void Function(R result) onSuccess,
    required String fallbackError,
  }) async {
    try {
      onSuccess(await change());
      notifyIfActive();
      return null;
    } on ApiException catch (e) {
      return adminErrorMessage(e, fallbackError);
    }
  }

  void _replace(AgentAccount updated) => _agents = [
    for (final agent in _agents) agent.id == updated.id ? updated : agent,
  ];

  static String? _blankAsNull(String text) {
    final trimmed = text.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  /// The server's order: case-insensitive by name.
  static List<AgentAccount> _sorted(List<AgentAccount> agents) =>
      [...agents]
        ..sort((a, b) => a.name.toLowerCase().compareTo(b.name.toLowerCase()));
}
