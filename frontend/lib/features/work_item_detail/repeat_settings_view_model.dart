import 'package:injectable/injectable.dart';
import 'package:weaver/core/dates/calendar_days.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/shared/recurrence/data/recurrence_repository.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/work_item_recurrence.dart';

/// The detail dialog's Repeat section. Kept apart from
/// `WorkItemDetailViewModel` because it's a separate resource with its own
/// load, save and error state. Like the details form, edits are held as a
/// [draft] and only sent on [save]: saving each change as it happens would
/// send invalid in-between states such as "weekly on no days".
@injectable
class RepeatSettingsViewModel extends ViewModel {
  RepeatSettingsViewModel(this._repository);

  final RecurrenceRepository _repository;

  String? _workItemId;
  bool _isOccurrence = false;
  WorkItemRecurrence? _saved;
  WorkItemRecurrence? _source;
  RecurrenceDraft? _draft;
  bool _isLoading = true;
  String? _loadError;
  bool _isSaving = false;
  String? _saveError;
  bool _hasChanges = false;

  bool get isLoading => _isLoading;
  String? get loadError => _loadError;
  bool get isSaving => _isSaving;
  String? get saveError => _saveError;

  /// Whether this item was created by another item's repetition. Its own
  /// schedule can't be set (the server rejects it); [source] describes the
  /// repetition it came from instead.
  bool get isOccurrence => _isOccurrence;

  /// The repetition this occurrence came from, or null if that item no
  /// longer repeats.
  WorkItemRecurrence? get source => _source;

  /// This item's saved schedule, or null if it doesn't repeat.
  WorkItemRecurrence? get saved => _saved;

  /// The settings being edited; null when the item doesn't repeat and the
  /// user hasn't started setting it up.
  RecurrenceDraft? get draft => _draft;

  /// True once a save or stop succeeded. Saving can create items on the
  /// board, so the caller should refresh.
  bool get hasChanges => _hasChanges;

  bool get isDirty {
    final draft = _draft;
    if (draft == null) return false;
    final saved = _saved;
    return saved == null || !draft.isEquivalentTo(saved.toDraft());
  }

  bool get canSave => isDirty && !_isSaving && _draft?.validationError == null;

  Future<void> load({
    required String workItemId,
    String? recurrenceSourceId,
  }) async {
    _workItemId = workItemId;
    _isOccurrence = recurrenceSourceId != null;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();

    try {
      if (recurrenceSourceId != null) {
        _source = await _repository.get(recurrenceSourceId);
      } else {
        _saved = await _repository.get(workItemId);
        _draft = _saved?.toDraft();
      }
    } on ApiException {
      _loadError = 'Could not load the repeat settings.';
    } finally {
      _isLoading = false;
      notifyIfActive();
    }
  }

  /// Starts a draft: weekly on the item's start date (or [today]), no end.
  void startRepeating({DateTime? itemStartDate, DateTime? today}) {
    _draft = RecurrenceDraft.startingOn(
      dateOnly(itemStartDate ?? today ?? DateTime.now()),
    );
    _saveError = null;
    notifyIfActive();
  }

  void updateDraft(RecurrenceDraft draft) {
    _draft = draft;
    _saveError = null;
    notifyIfActive();
  }

  void discardChanges() {
    _draft = _saved?.toDraft();
    _saveError = null;
    notifyIfActive();
  }

  Future<bool> save() async {
    final draft = _draft;
    if (draft == null) return false;
    final error = draft.validationError;
    if (error != null) {
      _saveError = error;
      notifyIfActive();
      return false;
    }
    return _run(() async {
      final saved = await _repository.save(_workItemId!, draft);
      _saved = saved;
      _draft = saved.toDraft();
    }, errorMessage: 'Could not save the repeat settings. Try again.');
  }

  Future<bool> stopRepeating() => _run(() async {
    await _repository.remove(_workItemId!);
    _saved = null;
    _draft = null;
  }, errorMessage: 'Could not stop this item repeating. Try again.');

  Future<bool> _run(
    Future<void> Function() action, {
    required String errorMessage,
  }) async {
    _isSaving = true;
    _saveError = null;
    notifyIfActive();
    try {
      await action();
      _hasChanges = true;
      return true;
    } on ApiException catch (e) {
      // A 400 carries the server's validation message.
      _saveError = e.statusCode == 400 ? e.message : errorMessage;
      return false;
    } finally {
      _isSaving = false;
      notifyIfActive();
    }
  }
}
