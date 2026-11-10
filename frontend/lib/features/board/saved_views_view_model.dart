import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/board/data/view_repository.dart';
import 'package:weaver/features/board/models/saved_view.dart';
import 'package:weaver/features/board/models/view_definition.dart';

/// The shared Views for the header's View menu: listing them, saving,
/// renaming and deleting one, and the signed-in user's default. Opening a
/// View is the board's job ([BoardViewModel.openView]).
@injectable
class SavedViewsViewModel extends ViewModel {
  SavedViewsViewModel(this._repository);

  final ViewRepository _repository;

  List<SavedView> _views = const [];
  String? _defaultViewId;
  String? _error;

  /// Overview first, then the saved Views by name.
  List<SavedView> get views => _views;

  String? get defaultViewId => _defaultViewId;

  /// Why the last action failed, for a snackbar; cleared by [clearError].
  String? get error => _error;

  void clearError() => _error = null;

  Future<void> load() => _run(() async {
    final views = await _repository.list();
    final defaultView = await _repository.loadDefault();
    _views = views;
    _defaultViewId = defaultView.id;
  }, failure: 'Could not load the Views. Check your connection.');

  /// The saved View, or null when it couldn't be saved ([error] says why).
  Future<SavedView?> create(ViewDefinition definition) => _run(() async {
    final view = await _repository.create(definition);
    _views = _sorted([..._views, view]);
    return view;
  }, failure: 'Could not save "${definition.name}". Try again.');

  /// [view] with [definition] in place of its own, or null when it couldn't
  /// be changed.
  Future<SavedView?> update(SavedView view, ViewDefinition definition) =>
      _run(() async {
        final updated = await _repository.update(view.id, definition);
        _views = _sorted([
          for (final existing in _views)
            existing.id == view.id ? updated : existing,
        ]);
        return updated;
      }, failure: 'Could not change "${view.name}". Try again.');

  Future<SavedView?> rename(SavedView view, String name) =>
      update(view, view.definition.renamed(name));

  /// True once deleted. Whoever had it as their default falls back to
  /// Overview.
  Future<bool> delete(SavedView view) async =>
      await _run(() async {
        await _repository.delete(view.id);
        _views = [
          for (final existing in _views)
            if (existing.id != view.id) existing,
        ];
        if (_defaultViewId == view.id) _defaultViewId = _overviewId;
        return true;
      }, failure: 'Could not delete "${view.name}". Try again.') ??
      false;

  Future<void> setDefault(SavedView view) => _run(() async {
    _defaultViewId = (await _repository.setDefault(view.id)).id;
  }, failure: 'Could not make "${view.name}" your default. Try again.');

  String? get _overviewId {
    for (final view in _views) {
      if (view.builtIn) return view.id;
    }
    return null;
  }

  static List<SavedView> _sorted(List<SavedView> views) => [
    for (final view in views)
      if (view.builtIn) view,
    ...[
      for (final view in views)
        if (!view.builtIn) view,
    ]..sort((a, b) => a.name.toLowerCase().compareTo(b.name.toLowerCase())),
  ];

  /// Runs [action], notifying after; a failure sets [error] (the server's
  /// own reason for a conflict, such as a name already taken) and yields
  /// null.
  Future<T?> _run<T>(
    Future<T> Function() action, {
    required String failure,
  }) async {
    try {
      final result = await action();
      notifyIfActive();
      return result;
    } on ApiException catch (e) {
      _error = e.isConflict ? e.detail ?? failure : failure;
      notifyIfActive();
      return null;
    }
  }
}
