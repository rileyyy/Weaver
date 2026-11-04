import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/quick_pick_dates.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/shared/data/status_repository.dart';
import 'package:weaver/shared/data/type_repository.dart';
import 'package:weaver/shared/data/user_directory_repository.dart';
import 'package:weaver/shared/data/work_item_search_repository.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

@LazySingleton(as: WorkItemDetailRepository)
class ApiWorkItemDetailRepository implements WorkItemDetailRepository {
  ApiWorkItemDetailRepository(
    http.Client client,
    ApiEndpoint endpoint,
    this._statuses,
    this._types,
    this._users,
    this._search,
  ) : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;
  final StatusRepository _statuses;
  final TypeRepository _types;
  final UserDirectoryRepository _users;
  final WorkItemSearchRepository _search;

  static WorkItem _detail(Object? json) =>
      WorkItem.fromJson(json as Map<String, dynamic>);

  /// Every write answers with the item and the fields it changed; the
  /// dialog only needs the item, which carries the new version.
  static WorkItem _writtenItem(Object? json) =>
      _detail((json as Map<String, dynamic>)['item']);

  @override
  Future<WorkItem> getItem(String id) => _api.get(
    '/work-items/$id',
    _detail,
    failureMessage: 'Failed to load work item',
  );

  @override
  Future<List<WorkItemChildSummary>> loadChildren(String parentId) => _api.get(
    '/work-items',
    (json) => JsonApiClient.listOf(json, WorkItemChildSummary.fromJson),
    query: {'parentId': parentId},
    failureMessage: 'Failed to load sub-items',
  );

  @override
  Future<void> deleteItem(
    String id, {
    bool cascade = false,
    required int expectedVersion,
  }) => _api.delete(
    '/work-items/$id',
    query: {'cascade': '$cascade', 'expectedVersion': '$expectedVersion'},
    failureMessage: 'Failed to delete work item',
  );

  @override
  Future<List<WorkItemStatus>> loadStatuses(String spaceId) =>
      _statuses.loadStatuses(spaceId);

  @override
  Future<List<WorkItemType>> loadTypes(String spaceId) =>
      _types.loadTypes(spaceId);

  @override
  Future<List<WorkItemPriority>> loadPriorities(String spaceId) => _api.get(
    '/work-item-priorities',
    (json) => JsonApiClient.listOf(json, WorkItemPriority.fromJson),
    query: {'spaceId': spaceId},
    failureMessage: 'Failed to load priorities',
  );

  @override
  Future<List<User>> loadUsers() => _users.loadUsers();

  @override
  Future<WorkItem> updateDetails(
    String id, {
    required String title,
    required String? description,
    required String? typeId,
    required String? priorityId,
    required int expectedVersion,
  }) => _api.patch(
    '/work-items/$id/details',
    {
      'title': title,
      'description': description,
      'typeId': typeId,
      'priorityId': priorityId,
      'expectedVersion': expectedVersion,
    },
    _writtenItem,
    failureMessage: 'Failed to update work item',
  );

  @override
  Future<WorkItem> updateDescription(
    String id,
    String description, {
    required int expectedVersion,
  }) => _api.patch(
    '/work-items/$id/details',
    {'description': description, 'expectedVersion': expectedVersion},
    _writtenItem,
    failureMessage: 'Failed to update work item',
  );

  @override
  Future<WorkItem> updateWhen(
    String id,
    String? when, {
    required int expectedVersion,
  }) => _api.patch(
    '/work-items/$id/details',
    {'when': when, 'expectedVersion': expectedVersion},
    _writtenItem,
    failureMessage: 'Failed to update work item',
  );

  @override
  Future<List<QuickPickDates>> loadQuickPicks() => _api.get(
    '/quick-picks',
    (json) => JsonApiClient.listOf(json, QuickPickDates.fromJson),
    failureMessage: 'Failed to load quick picks',
  );

  @override
  Future<WorkItem> assign(
    String id,
    String? userId, {
    required int expectedVersion,
  }) => _api.put(
    '/work-items/$id/assignee',
    {'userId': userId, 'expectedVersion': expectedVersion},
    _writtenItem,
    failureMessage: 'Failed to assign work item',
  );

  @override
  Future<WorkItem> reschedule(
    String id,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  }) => _api.patch(
    '/work-items/$id/schedule',
    {
      'startDate': formatCalendarDate(startDate),
      'endDate': formatCalendarDate(endDate),
      'expectedVersion': expectedVersion,
    },
    _writtenItem,
    failureMessage: 'Failed to reschedule work item',
  );

  @override
  Future<List<CustomField>> loadCustomFields(String typeId) => _api.get(
    '/custom-fields',
    (json) => JsonApiClient.listOf(json, CustomField.fromJson),
    query: {'typeId': typeId},
    failureMessage: 'Failed to load custom fields',
  );

  @override
  Future<WorkItem> setCustomFields(
    String id,
    Map<String, Object?> values, {
    required int expectedVersion,
  }) => _api.patch(
    '/work-items/$id/fields',
    {'values': values, 'expectedVersion': expectedVersion},
    _writtenItem,
    failureMessage: 'Failed to save custom fields',
  );

  @override
  Future<WorkItem> changeStatus(
    String id,
    String statusId, {
    required int expectedVersion,
  }) => _api.put(
    '/work-items/$id/status',
    {'statusId': statusId, 'afterId': null, 'expectedVersion': expectedVersion},
    _writtenItem,
    failureMessage: 'Failed to change status',
  );

  @override
  Future<WorkItem> reparent(
    String id,
    String? parentId, {
    required int expectedVersion,
  }) => _api.put(
    '/work-items/$id/parent',
    {'parentId': parentId, 'afterId': null, 'expectedVersion': expectedVersion},
    _writtenItem,
    failureMessage: 'Failed to change the parent',
  );

  @override
  Future<WorkItemChildSummary> createSubItem(
    String parentId, {
    required String title,
    required String statusId,
  }) => _api.post(
    '/work-items',
    {
      'title': title,
      'description': null,
      'parentId': parentId,
      'statusId': statusId,
    },
    (json) => WorkItemChildSummary.fromJson(json as Map<String, dynamic>),
    failureMessage: 'Failed to create sub-item',
  );

  @override
  Future<List<WorkItem>> searchWorkItems(String spaceId, String text) =>
      _search.search(spaceId: spaceId, text: text);

  @override
  Future<WorkItem> updateTags(
    String id,
    List<String> tags, {
    required int expectedVersion,
  }) => _api.patch(
    '/work-items/$id/tags',
    {'tags': tags, 'expectedVersion': expectedVersion},
    _writtenItem,
    failureMessage: 'Failed to set tags',
  );

  @override
  Future<List<WorkItemComment>> loadComments(String workItemId) => _api.get(
    '/work-items/$workItemId/comments',
    (json) => JsonApiClient.listOf(json, WorkItemComment.fromJson),
    failureMessage: 'Failed to load comments',
  );

  @override
  Future<WorkItemComment> addComment(String workItemId, String body) =>
      _api.post(
        '/work-items/$workItemId/comments',
        {'body': body},
        (json) => WorkItemComment.fromJson(json as Map<String, dynamic>),
        failureMessage: 'Failed to add comment',
      );

  @override
  Future<WorkItemComment> updateComment(String commentId, String body) =>
      _api.put(
        '/comments/$commentId',
        {'body': body},
        (json) => WorkItemComment.fromJson(json as Map<String, dynamic>),
        failureMessage: 'Failed to update comment',
      );

  @override
  Future<void> deleteComment(String commentId) => _api.delete(
    '/comments/$commentId',
    failureMessage: 'Failed to delete comment',
  );

  @override
  Future<List<WorkItemLink>> loadLinks(String workItemId) => _api.get(
    '/work-items/$workItemId/links',
    (json) => JsonApiClient.listOf(json, WorkItemLink.fromJson),
    failureMessage: 'Failed to load links',
  );

  @override
  Future<WorkItemLink> addLink(String workItemId, String targetWorkItemId) =>
      _api.post(
        '/work-items/$workItemId/links',
        {'targetWorkItemId': targetWorkItemId},
        (json) => WorkItemLink.fromJson(json as Map<String, dynamic>),
        failureMessage: 'Failed to add link',
      );

  @override
  Future<void> deleteLink(String linkId) => _api.delete(
    '/work-item-links/$linkId',
    failureMessage: 'Failed to delete link',
  );
}
