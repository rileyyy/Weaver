import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/models/quick_pick_dates.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link_type.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/models/work_item_url_link.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

abstract class WorkItemDetailRepository {
  Future<WorkItem> getItem(String id);

  /// This item's direct children, for the detail screen's "Sub-Items"
  /// section — the same `parentId`-scoped `/work-items` query the board
  /// uses for a swimlane's cards.
  Future<List<WorkItemChildSummary>> loadChildren(String parentId);

  /// Deletes the work item identified by [id]. If it has children,
  /// [cascade] must be true or the backend rejects the delete — a subtree
  /// is never silently dropped.
  Future<void> deleteItem(
    String id, {
    bool cascade = false,
    required int expectedVersion,
  });

  /// The statuses of the Space [spaceId]; an item can only take its own
  /// Space's.
  Future<List<WorkItemStatus>> loadStatuses(String spaceId);

  /// The Types of the Space [spaceId], as for [loadStatuses].
  Future<List<WorkItemType>> loadTypes(String spaceId);

  /// The priorities of the Space [spaceId], as for [loadStatuses].
  Future<List<WorkItemPriority>> loadPriorities(String spaceId);

  Future<List<User>> loadUsers();

  /// The custom fields the Type [typeId] defines, in order.
  Future<List<CustomField>> loadCustomFields(String typeId);

  /// Sets the custom field values in [values], by field id: a null value
  /// clears that field, and fields left out keep their value.
  Future<WorkItem> setCustomFields(
    String id,
    Map<String, Object?> values, {
    required int expectedVersion,
  });

  /// [expectedVersion] is the [WorkItem.version] the edit was based
  /// on; the save fails with a 409 [ApiException] if the item has changed
  /// since. Every write here takes one, including [deleteItem].
  Future<WorkItem> updateDetails(
    String id, {
    required String title,
    required String? description,
    required String? typeId,
    required String? priorityId,
    required int expectedVersion,
  });

  /// Saves just the description, leaving the title, type and priority as
  /// they are on the server, for edits that don't come from the details
  /// form (ticking a task).
  Future<WorkItem> updateDescription(
    String id,
    String description, {
    required int expectedVersion,
  });

  /// Saves just the When phrase (null clears it), leaving the other
  /// details as they are on the server.
  Future<WorkItem> updateWhen(
    String id,
    String? when, {
    required int expectedVersion,
  });

  /// The quick picks with their dates for today in the server's time zone.
  Future<List<QuickPickDates>> loadQuickPicks();

  Future<WorkItem> assign(
    String id,
    String? userId, {
    required int expectedVersion,
  });

  Future<WorkItem> reschedule(
    String id,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  });

  /// Moves the item to [statusId], at the top of that status in its lane.
  Future<WorkItem> changeStatus(
    String id,
    String statusId, {
    required int expectedVersion,
  });

  /// Moves the item under [parentId] (null makes it top-level). The server
  /// rejects a parent that is the item or one of its descendants with a
  /// 409, as it does a parent in another Space.
  Future<WorkItem> reparent(
    String id,
    String? parentId, {
    required int expectedVersion,
  });

  /// Creates a child of [parentId] in its Space with just a title.
  Future<WorkItemChildSummary> createSubItem(
    String parentId, {
    required String title,
    required String statusId,
  });

  /// The first page of items in [spaceId] matching [text], for a picker.
  Future<List<WorkItem>> searchWorkItems(String spaceId, String text);

  /// The first page of items in every Space matching [text], for a picker
  /// whose pick may be in another Space, such as a link's target.
  Future<List<WorkItem>> searchAllWorkItems(String text);

  /// Replaces this work item's full tag list.
  Future<WorkItem> updateTags(
    String id,
    List<String> tags, {
    required int expectedVersion,
  });

  Future<List<WorkItemComment>> loadComments(String workItemId);

  Future<WorkItemComment> addComment(String workItemId, String body);

  Future<WorkItemComment> updateComment(String commentId, String body);

  Future<void> deleteComment(String commentId);

  Future<List<WorkItemLink>> loadLinks(String workItemId);

  /// Links [workItemId] to [targetWorkItemId] as [type], read from
  /// [workItemId]'s side. The server refuses a self link, a repeat of an
  /// existing link and a blocks cycle with a 409 that says which.
  Future<WorkItemLink> addLink(
    String workItemId,
    String targetWorkItemId,
    WorkItemLinkType type,
  );

  Future<void> deleteLink(String linkId);

  Future<List<WorkItemUrlLink>> loadUrlLinks(String workItemId);

  /// The server refuses a [url] that isn't an absolute http(s) URL with a
  /// 400.
  Future<WorkItemUrlLink> addUrlLink(
    String workItemId,
    String url, {
    String? label,
  });

  Future<void> deleteUrlLink(String urlLinkId);
}
