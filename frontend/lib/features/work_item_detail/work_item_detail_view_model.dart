import 'dart:async';
import 'dart:ui' show Color;

import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/parallel_api_errors.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/quick_pick.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/shared/data/current_user.dart';
import 'package:weaver/shared/data/work_item_conflict.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/markdown/task_list.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

/// The item itself is read from and saved through the shared
/// [WorkItemStore], so every save sends the version it was based on and the
/// board shows it as soon as it's saved.
@injectable
class WorkItemDetailViewModel extends ViewModel {
  WorkItemDetailViewModel(this._repository, this._currentUser, this._items);

  final WorkItemDetailRepository _repository;
  final CurrentUser _currentUser;
  final WorkItemStore _items;

  String? _itemId;
  List<WorkItemStatus> _statuses = const [];
  List<WorkItemType> _types = const [];
  List<WorkItemPriority> _priorities = const [];
  List<CustomField> _customFields = const [];
  String? _customFieldsTypeId;
  List<User> _users = const [];
  List<WorkItemComment> _comments = const [];
  List<WorkItemLink> _links = const [];
  List<WorkItemChildSummary> _children = const [];
  bool _isLoading = true;
  String? _loadError;
  // A count, not a bool: saves can overlap, and the first one to finish
  // mustn't clear the flag while another is still in flight.
  int _pendingSaves = 0;
  String? _saveError;
  int _reloadGeneration = 0;
  bool _hasChanges = false;

  /// Null until [load] succeeds, and again once the item is deleted.
  WorkItem? get item => switch (_itemId) {
    final id? => _items[id],
    null => null,
  };

  String get _id => _itemId!;
  List<WorkItemStatus> get statuses => _statuses;
  List<WorkItemType> get types => _types;
  List<WorkItemPriority> get priorities => _priorities;

  /// The fields the item's saved Type defines, in order; empty without a
  /// Type.
  List<CustomField> get customFields => _customFields;
  List<User> get users => _users;
  List<WorkItemComment> get comments => _comments;
  List<WorkItemLink> get links => _links;
  List<WorkItemChildSummary> get children => _children;
  bool get isLoading => _isLoading;
  String? get loadError => _loadError;
  bool get isSaving => _pendingSaves > 0;
  String? get saveError => _saveError;

  /// Increments whenever [item] is replaced by a fresh server copy that
  /// should overwrite any unsaved edits in the view (after a save conflict).
  /// The view re-seeds its form fields when this changes; otherwise its
  /// stale values would overwrite the other person's change on the next save.
  int get reloadGeneration => _reloadGeneration;

  /// True once a sub-item dialog opened from this one reported a change the
  /// shared store can't show by itself (see [onNestedDialogClosed]), so the
  /// caller knows to refresh when this dialog closes. Saving or deleting the
  /// item doesn't count: the store already carries that to every view.
  bool get hasChanges => _hasChanges;

  /// Only the author may edit or delete a comment (the server enforces it
  /// too); this decides whether to offer the controls.
  bool isOwnComment(WorkItemComment comment) =>
      comment.authorUserId == _currentUser.currentUserId;

  String? get statusName {
    for (final status in _statuses) {
      if (status.id == item?.statusId) return status.name;
    }
    return null;
  }

  /// Looks up a status's configured color by id — used to mark each
  /// sub-item's status in the "Sub-Items" section without duplicating the
  /// loaded status list into the view.
  Color? statusColorFor(String statusId) {
    for (final status in _statuses) {
      if (status.id == statusId) return status.color;
    }
    return null;
  }

  static List<AsyncError?> _asyncErrors(Object? errors) => switch (errors) {
    (final AsyncError? a, final AsyncError? b, final AsyncError? c) => [
      a,
      b,
      c,
    ],
    (
      final AsyncError? a,
      final AsyncError? b,
      final AsyncError? c,
      final AsyncError? d,
    ) =>
      [a, b, c, d],
    (
      final AsyncError? a,
      final AsyncError? b,
      final AsyncError? c,
      final AsyncError? d,
      final AsyncError? e,
    ) =>
      [a, b, c, d, e],
    _ => throw StateError('Unexpected parallel wait errors: $errors'),
  };

  Future<void> load(String id) async {
    _isLoading = true;
    _loadError = null;
    notifyIfActive();

    try {
      final asOf = _items.revision;
      final (item, users, comments, links, children) = await (
        _repository.getItem(id),
        _repository.loadUsers(),
        _repository.loadComments(id),
        _repository.loadLinks(id),
        _repository.loadChildren(id),
      ).wait;
      // Only the item says which Space's statuses, Types and priorities it
      // can take.
      final (statuses, types, priorities, customFields) = await (
        _repository.loadStatuses(item.spaceId),
        _repository.loadTypes(item.spaceId),
        _repository.loadPriorities(item.spaceId),
        _loadCustomFields(item.typeId),
      ).wait;
      _items.putAll([item], asOf: asOf);
      _itemId = id;
      _statuses = statuses;
      _types = types;
      _priorities = priorities;
      _customFields = customFields;
      _customFieldsTypeId = item.typeId;
      _users = users;
      _comments = comments;
      _links = links;
      _children = children;
    } on ParallelWaitError<Object?, Object?> catch (e) {
      rethrowNonApiErrors(_asyncErrors(e.errors));
      _loadError =
          'Could not load this work item. Check your connection and try again.';
    } finally {
      _isLoading = false;
      notifyIfActive();
    }
  }

  /// Why [title] can't be saved, or null if it can. The server enforces
  /// the same rules; checking here lets the view say so before a round trip.
  String? titleError(String title) {
    if (title.trim().isEmpty) return 'Title is required.';
    if (title.length > titleMaxLength) {
      return 'Title must be at most $titleMaxLength characters.';
    }
    return null;
  }

  /// Mirrors `WorkItem.TitleMaxLength` on the backend.
  static const int titleMaxLength = 500;

  Future<bool> saveDetails({
    required String title,
    required String? description,
    required String? typeId,
    required String? priorityId,
  }) {
    final error = titleError(title);
    if (error != null) return _reject(error);
    return _save(
      (version) => _repository.updateDetails(
        _id,
        title: title,
        description: description,
        typeId: typeId,
        priorityId: priorityId,
        expectedVersion: version,
      ),
    );
  }

  /// Whether saving [typeId] would clear values the item holds: the server
  /// drops them, since another Type's fields are other fields.
  bool typeChangeClearsFields(String? typeId) =>
      typeId != item?.typeId && (item?.fields.isNotEmpty ?? false);

  /// Saves one custom field's value (null clears it) on its own, like the
  /// other metadata fields. The server checks it against the field.
  Future<bool> saveCustomField(String fieldId, Object? value) => _save(
    (version) => _repository.setCustomFields(_id, {
      fieldId: value,
    }, expectedVersion: version),
  );

  Future<List<CustomField>> _loadCustomFields(String? typeId) async =>
      typeId == null ? const [] : _repository.loadCustomFields(typeId);

  /// A save, or the latest version a conflict brings in, can leave the item
  /// with another Type, whose fields are other fields.
  Future<void> _followType() async {
    final typeId = item?.typeId;
    if (item == null || typeId == _customFieldsTypeId) return;
    _customFieldsTypeId = typeId;
    try {
      _customFields = await _loadCustomFields(typeId);
    } on ApiException {
      // The old Type's fields no longer apply to the item.
      _customFields = const [];
      _saveError ??=
          "This Type's fields could not be loaded. Reopen the "
          'item to edit them.';
    }
    notifyIfActive();
  }

  /// Ticks or unticks the [taskIndex]th task line of the saved
  /// description (counted as in [toggleTask]) and saves only the
  /// description, so a tick never carries unsaved form edits with it.
  Future<bool> toggleDescriptionTask(int taskIndex) {
    final description = toggleTask(item!.description ?? '', taskIndex);
    return _save(
      (version) => _repository.updateDescription(
        _id,
        description,
        expectedVersion: version,
      ),
    );
  }

  Future<bool> saveAssignee(String? userId) => _save(
    (version) => _repository.assign(_id, userId, expectedVersion: version),
  );

  /// Deletes this work item and any sub-items — the confirmation dialog
  /// already warns the user about the subtree, so this always cascades
  /// rather than asking a second time.
  ///
  /// The item leaves the shared store, so every layout drops it.
  Future<bool> deleteItem() async {
    // Queued through the store like a save, so a delete right after a save
    // sends the version that save returned rather than a stale one.
    final deleted = await _save((version) async {
      await _repository.deleteItem(
        _id,
        cascade: true,
        expectedVersion: version,
      );
      return item!;
    }, errorMessage: 'Could not delete this work item. Try again.');
    if (deleted) _items.remove(_id);
    return deleted;
  }

  Future<bool> saveTags(List<String> tags) => _save(
    (version) => _repository.updateTags(_id, tags, expectedVersion: version),
  );

  Future<bool> saveSchedule(DateTime? startDate, DateTime? endDate) {
    if (startDate != null && endDate != null && startDate.isAfter(endDate)) {
      return _reject('The start date must not be after the end date.');
    }
    return _save(
      (version) => _repository.reschedule(
        _id,
        startDate,
        endDate,
        expectedVersion: version,
      ),
    );
  }

  /// Saves the When phrase on its own, apart from the details form; a blank
  /// phrase clears it. Typing a When never sets dates.
  Future<bool> saveWhen(String text) {
    final trimmed = text.trim();
    final when = trimmed.isEmpty ? null : trimmed;
    if (when == item!.when) return Future.value(true);
    return _save(
      (version) => _repository.updateWhen(_id, when, expectedVersion: version),
    );
  }

  /// Sets the dates the server resolves [pick] to. They're fetched on each
  /// tap rather than at load, so a dialog left open past midnight still
  /// gets today's dates.
  Future<bool> applyQuickPick(QuickPick pick) => _save((version) async {
    final picks = await _repository.loadQuickPicks();
    final dates = picks.firstWhere((p) => p.pick == pick);
    return _repository.reschedule(
      _id,
      dates.startDate,
      dates.endDate,
      expectedVersion: version,
    );
  });

  Future<bool> _reject(String message) {
    _saveError = message;
    notifyIfActive();
    return Future.value(false);
  }

  // Comment and link mutations apply the server's response locally instead
  // of reloading the list: otherwise a reload failing after a successful
  // POST would be reported as a failure, and a retry would create a
  // duplicate.

  Future<bool> addComment(String body) => _mutate(() async {
    final created = await _repository.addComment(_id, body);
    _comments = [..._comments, created];
  }, errorMessage: 'Could not add your comment. Try again.');

  Future<bool> updateComment(String commentId, String body) => _mutate(
    () async {
      final updated = await _repository.updateComment(commentId, body);
      _comments = [for (final c in _comments) c.id == commentId ? updated : c];
    },
    errorMessage: 'Could not update your comment. Try again.',
  );

  Future<bool> deleteComment(String commentId) => _mutate(() async {
    await _repository.deleteComment(commentId);
    _comments = [
      for (final c in _comments)
        if (c.id != commentId) c,
    ];
  }, errorMessage: 'Could not delete this comment. Try again.');

  Future<bool> addLink(String targetWorkItemId) => _mutate(
    () async {
      final created = await _repository.addLink(_id, targetWorkItemId);
      _links = [..._links, created];
    },
    errorMessage:
        'Could not add this link. Check the work item id and try again.',
  );

  Future<bool> deleteLink(String linkId) => _mutate(() async {
    await _repository.deleteLink(linkId);
    _links = [
      for (final l in _links)
        if (l.id != linkId) l,
    ];
  }, errorMessage: 'Could not remove this link. Try again.');

  /// Called when a nested dialog (a sub-item, or the repeating item that
  /// created this one) closes. The Sub-Items list is reloaded either way,
  /// since its titles and statuses may have been edited there; [changed]
  /// (the nested dialog's own result) marks this dialog as changed too.
  Future<void> onNestedDialogClosed({required bool changed}) async {
    if (changed) _hasChanges = true;
    try {
      _children = await _repository.loadChildren(_id);
    } on Exception {
      // Keep the current list; it only goes stale.
    }
    notifyIfActive();
  }

  Future<bool> _save(
    Future<WorkItem> Function(int expectedVersion) send, {
    String errorMessage = 'Could not save your change. Try again.',
  }) async {
    final saved = await _mutate(
      () =>
          _items.write(_id, send: send, reload: () => _repository.getItem(_id)),
      errorMessage: errorMessage,
    );
    await _followType();
    return saved;
  }

  void _reportConflict(WorkItemConflict conflict) {
    const notSaved =
        'Someone else changed this work item, so your change was not saved.';
    if (conflict.latest == null) {
      _saveError = '$notSaved Close and reopen it to see the latest version.';
      return;
    }
    _reloadGeneration++;
    _saveError = '$notSaved The latest version is now shown.';
  }

  Future<bool> _mutate(
    Future<void> Function() action, {
    required String errorMessage,
  }) async {
    _pendingSaves++;
    _saveError = null;
    notifyIfActive();

    try {
      await action();
      return true;
    } on WorkItemConflict catch (conflict) {
      _reportConflict(conflict);
      return false;
    } on ApiException catch (e) {
      // A 400 carries the server's validation message ("Each tag must be
      // at most 50 characters."), which says more than "try again".
      _saveError = e.statusCode == 400 ? e.message : errorMessage;
      return false;
    } finally {
      _pendingSaves--;
      notifyIfActive();
    }
  }
}
