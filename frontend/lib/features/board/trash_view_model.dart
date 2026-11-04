import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/board/data/trash_repository.dart';
import 'package:weaver/features/board/models/trashed_work_item.dart';

/// The board's Trash tab: each delete, with restore. Separate from
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

  /// Why the last restore failed, for a snackbar; cleared by
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
      _items = [
        for (final item in _items)
          if (item.workItemId != workItemId) item,
      ];
      notifyIfActive();
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

  void clearActionError() => _actionError = null;
}
