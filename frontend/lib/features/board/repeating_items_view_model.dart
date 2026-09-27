import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/shared/recurrence/data/recurrence_repository.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/work_item_recurrence.dart';

/// The board's Repeating tab: every repeating work item, with editing and
/// stopping. Separate from `BoardViewModel` because it shares none of the
/// board's scope, filters or optimistic moves.
@injectable
class RepeatingItemsViewModel extends ViewModel {
  RepeatingItemsViewModel(this._repository);

  final RecurrenceRepository _repository;

  List<WorkItemRecurrence> _items = const [];
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;
  String? _actionError;
  int _loadGeneration = 0;

  List<WorkItemRecurrence> get items => _items;

  /// Only true for the first load; later refreshes keep showing the list.
  bool get isLoading => _isLoading && !_hasLoaded;
  String? get loadError => _loadError;

  /// Why the last stop failed, for a snackbar; cleared by [clearActionError].
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
      _loadError = 'Could not load repeating items. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  /// Saves [draft] for [workItemId]. Returns null on success, or why it
  /// failed so the edit dialog can stay open and say so.
  Future<String?> save(String workItemId, RecurrenceDraft draft) async {
    final error = draft.validationError;
    if (error != null) return error;
    try {
      final saved = await _repository.save(workItemId, draft);
      _items = [
        for (final item in _items) item.workItemId == workItemId ? saved : item,
      ];
      notifyIfActive();
      return null;
    } on ApiException catch (e) {
      return e.statusCode == 400
          ? e.message
          : 'Could not save the repeat settings. Try again.';
    }
  }

  Future<bool> stopRepeating(String workItemId) async {
    try {
      await _repository.remove(workItemId);
      _items = [
        for (final item in _items)
          if (item.workItemId != workItemId) item,
      ];
      notifyIfActive();
      return true;
    } on ApiException {
      _actionError = 'Could not stop this item repeating. Try again.';
      notifyIfActive();
      return false;
    }
  }

  void clearActionError() => _actionError = null;
}
