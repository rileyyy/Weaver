import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/api_exception.dart';
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
  ApiWorkItemDetailRepository(this._client, @Named('apiBaseUrl') this._baseUrl);

  final http.Client _client;
  final String _baseUrl;

  @override
  Future<WorkItemDetail> getItem(String id) async {
    final response = await _client.get(_uri('/work-items/$id'));
    _checkOk(response, 'Failed to load work item');
    return _toDetail(jsonDecode(response.body) as Map<String, dynamic>);
  }

  @override
  Future<List<WorkItemChildSummary>> loadChildren(String parentId) async {
    final json = await _getJsonList('/work-items?parentId=$parentId');
    return [
      for (final item in json)
        WorkItemChildSummary(
          id: item['id'] as String,
          number: item['number'] as int,
          title: item['title'] as String,
          statusId: item['statusId'] as String,
        ),
    ];
  }

  @override
  Future<void> deleteItem(String id, {bool cascade = false}) async {
    final response = await _client.delete(_uri('/work-items/$id?cascade=$cascade'));
    _checkOk(response, 'Failed to delete work item');
  }

  @override
  Future<List<BoardStatus>> loadStatuses() async {
    final json = await _getJsonList('/statuses');
    return [
      for (final item in json)
        BoardStatus(
          id: item['id'] as String,
          name: item['name'] as String,
          order: item['order'] as int,
          color: parseStatusColor(item['color'] as String),
        ),
    ];
  }

  @override
  Future<List<WorkItemLayer>> loadLayers() async {
    final json = await _getJsonList('/work-item-layers');
    return [
      for (final item in json)
        WorkItemLayer(id: item['id'] as String, name: item['name'] as String, order: item['order'] as int),
    ];
  }

  @override
  Future<List<AuthUser>> loadUsers() async {
    final json = await _getJsonList('/users');
    return [
      for (final item in json)
        AuthUser(
          id: item['id'] as String,
          username: item['username'] as String,
          kind: userKindFromWire(item['kind'] as String),
        ),
    ];
  }

  @override
  Future<WorkItemDetail> updateDetails(
    String id, {
    required String title,
    required String? description,
    required String? layerId,
    required WorkItemPriority priority,
    required int expectedVersion,
  }) async {
    final response = await _client.put(
      _uri('/work-items/$id/details'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({
        'title': title,
        'description': description,
        'layerId': layerId,
        'priority': priority.toWire(),
        'expectedVersion': expectedVersion,
      }),
    );
    _checkOk(response, 'Failed to update work item');
    return _toDetail(jsonDecode(response.body) as Map<String, dynamic>);
  }

  @override
  Future<WorkItemDetail> assign(String id, String? userId) async {
    final response = await _client.post(
      _uri('/work-items/$id/assignee'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'userId': userId}),
    );
    _checkOk(response, 'Failed to assign work item');
    return _toDetail(jsonDecode(response.body) as Map<String, dynamic>);
  }

  @override
  Future<WorkItemDetail> reschedule(
    String id,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  }) async {
    final response = await _client.post(
      _uri('/work-items/$id/schedule'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({
        'startDate': formatCalendarDate(startDate),
        'endDate': formatCalendarDate(endDate),
        'expectedVersion': expectedVersion,
      }),
    );
    _checkOk(response, 'Failed to reschedule work item');
    return _toDetail(jsonDecode(response.body) as Map<String, dynamic>);
  }

  @override
  Future<WorkItemDetail> updateTags(String id, List<String> tags, {required int expectedVersion}) async {
    final response = await _client.post(
      _uri('/work-items/$id/tags'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'tags': tags, 'expectedVersion': expectedVersion}),
    );
    _checkOk(response, 'Failed to set tags');
    return _toDetail(jsonDecode(response.body) as Map<String, dynamic>);
  }

  @override
  Future<List<WorkItemComment>> loadComments(String workItemId) async {
    final json = await _getJsonList('/work-items/$workItemId/comments');
    return [for (final item in json) _toComment(item)];
  }

  @override
  Future<WorkItemComment> addComment(String workItemId, String body) async {
    final response = await _client.post(
      _uri('/work-items/$workItemId/comments'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'body': body}),
    );
    _checkOk(response, 'Failed to add comment');
    return _toComment(jsonDecode(response.body) as Map<String, dynamic>);
  }

  @override
  Future<WorkItemComment> updateComment(String commentId, String body) async {
    final response = await _client.put(
      _uri('/comments/$commentId'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'body': body}),
    );
    _checkOk(response, 'Failed to update comment');
    return _toComment(jsonDecode(response.body) as Map<String, dynamic>);
  }

  @override
  Future<void> deleteComment(String commentId) async {
    final response = await _client.delete(_uri('/comments/$commentId'));
    _checkOk(response, 'Failed to delete comment');
  }

  @override
  Future<List<WorkItemLink>> loadLinks(String workItemId) async {
    final json = await _getJsonList('/work-items/$workItemId/links');
    return [for (final item in json) _toLink(item)];
  }

  @override
  Future<WorkItemLink> addLink(String workItemId, String targetWorkItemId) async {
    final response = await _client.post(
      _uri('/work-items/$workItemId/links'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'targetWorkItemId': targetWorkItemId}),
    );
    _checkOk(response, 'Failed to add link');
    return _toLink(jsonDecode(response.body) as Map<String, dynamic>);
  }

  @override
  Future<void> deleteLink(String linkId) async {
    final response = await _client.delete(_uri('/work-item-links/$linkId'));
    _checkOk(response, 'Failed to delete link');
  }

  WorkItemComment _toComment(Map<String, dynamic> json) => WorkItemComment(
    id: json['id'] as String,
    workItemId: json['workItemId'] as String,
    authorUserId: json['authorUserId'] as String,
    authorUsername: json['authorUsername'] as String,
    body: json['body'] as String,
    createdAt: parseApiTimestamp(json['createdAtUtc'] as String),
    updatedAt: parseOptionalApiTimestamp(json['updatedAtUtc']),
  );

  WorkItemLink _toLink(Map<String, dynamic> json) => WorkItemLink(
    id: json['id'] as String,
    linkedWorkItemId: json['linkedWorkItemId'] as String,
    linkedWorkItemTitle: json['linkedWorkItemTitle'] as String,
  );

  WorkItemDetail _toDetail(Map<String, dynamic> json) => WorkItemDetail(
    id: json['id'] as String,
    parentId: json['parentId'] as String?,
    title: json['title'] as String,
    description: json['description'] as String?,
    statusId: json['statusId'] as String,
    layerId: json['layerId'] as String?,
    priority: workItemPriorityFromWire(json['priority'] as String),
    assignedToUserId: json['assignedToUserId'] as String?,
    startDate: parseCalendarDate(json['startDate']),
    endDate: parseCalendarDate(json['endDate']),
    createdAt: parseApiTimestamp(json['createdAtUtc'] as String),
    updatedAt: parseApiTimestamp(json['updatedAtUtc'] as String),
    version: json['version'] as int,
    tags: (json['tags'] as List<dynamic>?)?.cast<String>() ?? const [],
  );


  Future<List<Map<String, dynamic>>> _getJsonList(String path) async {
    final response = await _client.get(_uri(path));
    _checkOk(response, 'Failed to load $path');
    return (jsonDecode(response.body) as List<dynamic>).cast<Map<String, dynamic>>();
  }

  Uri _uri(String path) => Uri.parse('$_baseUrl$path');

  void _checkOk(http.Response response, String fallbackMessage) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    throw ApiException(
      '${_problemDetail(response) ?? fallbackMessage} (${response.statusCode}).',
      statusCode: response.statusCode,
    );
  }

  String? _problemDetail(http.Response response) {
    try {
      final body = jsonDecode(response.body);
      return body is Map<String, dynamic> ? body['detail'] as String? : null;
    } on FormatException {
      return null;
    }
  }
}
