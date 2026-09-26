import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_layer.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';

@LazySingleton(as: WorkItemDetailRepository)
class ApiWorkItemDetailRepository implements WorkItemDetailRepository {
  ApiWorkItemDetailRepository(
    http.Client client,
    @Named('apiBaseUrl') String baseUrl,
  ) : _api = JsonApiClient(client, baseUrl);

  final JsonApiClient _api;

  static WorkItemDetail _detail(Object? json) =>
      WorkItemDetail.fromJson(json as Map<String, dynamic>);

  @override
  Future<WorkItemDetail> getItem(String id) => _api.get(
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
  Future<void> deleteItem(String id, {bool cascade = false}) => _api.delete(
    '/work-items/$id',
    query: {'cascade': '$cascade'},
    failureMessage: 'Failed to delete work item',
  );

  @override
  Future<List<BoardStatus>> loadStatuses() => _api.get(
    '/statuses',
    (json) => JsonApiClient.listOf(json, BoardStatus.fromJson),
    failureMessage: 'Failed to load statuses',
  );

  @override
  Future<List<WorkItemLayer>> loadLayers() => _api.get(
    '/work-item-layers',
    (json) => JsonApiClient.listOf(json, WorkItemLayer.fromJson),
    failureMessage: 'Failed to load layers',
  );

  @override
  Future<List<AuthUser>> loadUsers() => _api.get(
    '/users',
    (json) => JsonApiClient.listOf(json, AuthUser.fromJson),
    failureMessage: 'Failed to load users',
  );

  @override
  Future<WorkItemDetail> updateDetails(
    String id, {
    required String title,
    required String? description,
    required String? layerId,
    required WorkItemPriority priority,
    required int expectedVersion,
  }) => _api.put(
    '/work-items/$id/details',
    {
      'title': title,
      'description': description,
      'layerId': layerId,
      'priority': priority.toWire(),
      'expectedVersion': expectedVersion,
    },
    _detail,
    failureMessage: 'Failed to update work item',
  );

  @override
  Future<WorkItemDetail> assign(String id, String? userId) => _api.post(
    '/work-items/$id/assignee',
    {'userId': userId},
    _detail,
    failureMessage: 'Failed to assign work item',
  );

  @override
  Future<WorkItemDetail> reschedule(
    String id,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  }) => _api.post(
    '/work-items/$id/schedule',
    {
      'startDate': formatCalendarDate(startDate),
      'endDate': formatCalendarDate(endDate),
      'expectedVersion': expectedVersion,
    },
    _detail,
    failureMessage: 'Failed to reschedule work item',
  );

  @override
  Future<WorkItemDetail> updateTags(
    String id,
    List<String> tags, {
    required int expectedVersion,
  }) => _api.post(
    '/work-items/$id/tags',
    {'tags': tags, 'expectedVersion': expectedVersion},
    _detail,
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
