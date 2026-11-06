import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/board/data/trash_repository.dart';
import 'package:weaver/features/board/models/trashed_work_item.dart';

/// The board's Trash tab: each delete, with restore and purge. Separate from
/// `BoardViewModel` because it shares none of the board's scope or filters.
@injectable
class TrashViewModel extends ViewModel {
  TrashViewModel(this._repository);

  final TrashRepository _repository;

  List<TrashedWorkItem> _items = const [];
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;
  String? _actionError;
  int _loadGeneration = 0;

  List<TrashedWorkItem> get items => _items;

  /// Only true for the first load; later refreshes keep showing the list.
  bool get isLoading => _isLoading && !_hasLoaded;
  String? get loadError => _loadError;

  /// Why the last restore or purge failed, for a snackbar; cleared by
  /// [clearActionError].
  String? get actionError => _actionError;

  Future<void> load() async {
    final generation = ++_loadGeneration;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      final items = await _repository.list();
      if (generation != _loadGeneration) return;
      _items = items;
      _hasLoaded = true;
    } on ApiException {
      if (generation != _loadGeneration) return;
      _loadError = 'Could not load the Trash. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  /// True when the item is back on the board.
  Future<bool> restore(String workItemId) async {
    try {
      await _repository.restore(workItemId);
      _removeItem(workItemId);
      return true;
    } on ApiException catch (e) {
      // A 400 says which item to restore instead, which the user needs.
      _actionError = e.statusCode == 400
          ? e.message
          : 'Could not restore this item. Try again.';
      notifyIfActive();
      return false;
    }
  }

  /// True when the item, and everything deleted with it, is gone for good.
  Future<bool> purge(String workItemId) async {
    try {
      await _repository.purge(workItemId);
      _removeItem(workItemId);
      return true;
    } on ApiException catch (e) {
      // Already purged, e.g. by retention, so the row is just stale.
      if (e.statusCode == 404) {
        _removeItem(workItemId);
        return true;
      }
      _reportActionError(
        e,
        'Could not delete this item permanently. Try again.',
      );
      return false;
    }
  }

  /// True when the Trash, in every Space, is empty.
  Future<bool> emptyTrash() async {
    try {
      await _repository.empty();
      _items = const [];
      notifyIfActive();
      return true;
    } on ApiException catch (e) {
      _reportActionError(e, 'Could not empty the Trash. Try again.');
      return false;
    }
  }

  void clearActionError() => _actionError = null;

  void _removeItem(String workItemId) {
    _items = [
      for (final item in _items)
        if (item.workItemId != workItemId) item,
    ];
    notifyIfActive();
  }

  /// A 400 says which item to purge instead and a 403 that this account
  /// may not purge, both of which the user needs.
  void _reportActionError(ApiException error, String fallback) {
    _actionError = switch (error.statusCode) {
      400 || 403 => error.message,
      _ => fallback,
    };
    notifyIfActive();
  }
}
