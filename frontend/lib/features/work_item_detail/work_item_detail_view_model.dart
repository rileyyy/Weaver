import 'dart:async';
import 'dart:ui' show Color;

import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/parallel_api_errors.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_layer.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/shared/data/current_user.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item_status.dart';

@injectable
class WorkItemDetailViewModel extends ViewModel {
  WorkItemDetailViewModel(this._repository, this._currentUser);

  final WorkItemDetailRepository _repository;
  final CurrentUser _currentUser;

  WorkItemDetail? _item;
  List<WorkItemStatus> _statuses = const [];
  List<WorkItemLayer> _layers = const [];
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

  WorkItemDetail? get item => _item;
  List<WorkItemStatus> get statuses => _statuses;
  List<WorkItemLayer> get layers => _layers;
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

  /// True once anything that other views show (fields, tags, schedule,
  /// assignee, deletion, or a sub-item) was changed from this dialog, so
  /// the caller knows to refresh when it closes.
  bool get hasChanges => _hasChanges;

  /// Only the author may edit or delete a comment (the server enforces it
  /// too); this decides whether to offer the controls.
  bool isOwnComment(WorkItemComment comment) =>
      comment.authorUserId == _currentUser.currentUserId;

  String? get statusName {
    for (final status in _statuses) {
      if (status.id == _item?.statusId) return status.name;
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

  Future<void> load(String id) async {
    _isLoading = true;
    _loadError = null;
    notifyIfActive();

    try {
      final (item, statuses, layers, users, comments, links, children) = await (
        _repository.getItem(id),
        _repository.loadStatuses(),
        _repository.loadLayers(),
        _repository.loadUsers(),
        _repository.loadComments(id),
        _repository.loadLinks(id),
        _repository.loadChildren(id),
      ).wait;
      _item = item;
      _statuses = statuses;
      _layers = layers;
      _users = users;
      _comments = comments;
      _links = links;
      _children = children;
    } on ParallelWaitError<Object?, Object?> catch (e) {
      final (a, b, c, d, f, g, h) =
          e.errors
              as (
                AsyncError?,
                AsyncError?,
                AsyncError?,
                AsyncError?,
                AsyncError?,
                AsyncError?,
                AsyncError?,
              );
      rethrowNonApiErrors([a, b, c, d, f, g, h]);
      _loadError =
          'Could not load this work item. Check your connection and try again.';
    } finally {
      _isLoading = false;
      notifyIfActive();
    }
  }

  Future<bool> saveDetails({
    required String title,
    required String? description,
    required String? layerId,
    required WorkItemPriority priority,
  }) => _save(
    () => _repository.updateDetails(
      _item!.id,
      title: title,
      description: description,
      layerId: layerId,
      priority: priority,
      expectedVersion: _item!.version,
    ),
  );

  Future<bool> saveAssignee(String? userId) =>
      _save(() => _repository.assign(_item!.id, userId));

  /// Deletes this work item and any sub-items — the confirmation dialog
  /// already warns the user about the subtree, so this always cascades
  /// rather than asking a second time.
  Future<bool> deleteItem() => _mutate(() async {
    await _repository.deleteItem(_item!.id, cascade: true);
    _hasChanges = true;
  }, errorMessage: 'Could not delete this work item. Try again.');

  Future<bool> saveTags(List<String> tags) => _save(
    () => _repository.updateTags(
      _item!.id,
      tags,
      expectedVersion: _item!.version,
    ),
  );

  Future<bool> saveSchedule(DateTime? startDate, DateTime? endDate) => _save(
    () => _repository.reschedule(
      _item!.id,
      startDate,
      endDate,
      expectedVersion: _item!.version,
    ),
  );

  // Comment and link mutations apply the server's response locally instead
  // of reloading the list: a reload that failed after a successful POST
  // used to be reported as a failure, and retrying created a duplicate.

  Future<bool> addComment(String body) => _mutate(() async {
    final created = await _repository.addComment(_item!.id, body);
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
      final created = await _repository.addLink(_item!.id, targetWorkItemId);
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

  /// Called when a sub-item's own dialog reports a change (e.g. it was
  /// deleted): reloads the Sub-Items list and marks this dialog as changed.
  Future<void> onSubItemChanged() async {
    _hasChanges = true;
    try {
      _children = await _repository.loadChildren(_item!.id);
    } on Exception {
      // Keep the current list; the caller's refresh still picks up the change.
    }
    notifyIfActive();
  }

  Future<bool> _save(Future<WorkItemDetail> Function() action) => _mutate(
    () async {
      _item = await action();
      _hasChanges = true;
    },
    errorMessage: 'Could not save your change. Try again.',
    onConflict: _reloadAfterConflict,
  );

  Future<void> _reloadAfterConflict() async {
    const notSaved =
        'Someone else changed this work item, so your change was not saved.';
    try {
      _item = await _repository.getItem(_item!.id);
      _reloadGeneration++;
      _saveError = '$notSaved The latest version is now shown.';
    } on Exception {
      _saveError = '$notSaved Close and reopen it to see the latest version.';
    }
  }

  Future<bool> _mutate(
    Future<void> Function() action, {
    required String errorMessage,
    Future<void> Function()? onConflict,
  }) async {
    _pendingSaves++;
    _saveError = null;
    notifyIfActive();

    try {
      await action();
      return true;
    } on ApiException catch (e) {
      if (e.isConflict && onConflict != null) {
        await onConflict();
      } else {
        _saveError = errorMessage;
      }
      return false;
    } finally {
      _pendingSaves--;
      notifyIfActive();
    }
  }
}
