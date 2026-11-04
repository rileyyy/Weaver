import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/admin/data/space_admin_repository.dart';
import 'package:weaver/features/admin/state/admin_error_message.dart';
import 'package:weaver/shared/models/space.dart';

/// The Admin page's Spaces tab. Each change returns null on success, or why
/// it failed for the dialog that asked for it. Spaces can't be deleted yet:
/// nothing is in place to say what happens to their items.
@injectable
class SpaceAdminViewModel extends ViewModel {
  SpaceAdminViewModel(this._repository);

  final SpaceAdminRepository _repository;

  List<Space> _spaces = const [];
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;
  int _loadGeneration = 0;

  List<Space> get spaces => _spaces;

  /// Only true for the first load; later refreshes keep showing the list.
  bool get isLoading => _isLoading && !_hasLoaded;
  String? get loadError => _loadError;

  Future<void> load() async {
    final generation = ++_loadGeneration;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      final spaces = await _repository.list();
      if (generation != _loadGeneration) return;
      _spaces = spaces;
      _hasLoaded = true;
    } on ApiException {
      if (generation != _loadGeneration) return;
      _loadError = 'Could not load the Spaces. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  Future<String?> create(String name) {
    final trimmed = name.trim();
    if (trimmed.isEmpty) return Future.value('Enter a name.');
    return _attempt(
      () => _repository.create(trimmed),
      onSuccess: (created) => _spaces = _sorted([..._spaces, created]),
      fallbackError: 'Could not add the Space. Try again.',
    );
  }

  Future<String?> rename(String id, String name) {
    final trimmed = name.trim();
    if (trimmed.isEmpty) return Future.value('Enter a name.');
    return _attempt(
      () => _repository.rename(id, trimmed),
      onSuccess: (renamed) => _spaces = _sorted([
        for (final space in _spaces) space.id == renamed.id ? renamed : space,
      ]),
      fallbackError: 'Could not rename the Space. Try again.',
    );
  }

  Future<String?> _attempt(
    Future<Space> Function() change, {
    required void Function(Space result) onSuccess,
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

  /// Close to the server's collation-ordered list, so the order doesn't jump
  /// on the next load.
  static List<Space> _sorted(List<Space> spaces) =>
      [...spaces]
        ..sort((a, b) => a.name.toLowerCase().compareTo(b.name.toLowerCase()));
}
