import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_layer.dart';
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
  Future<List<BoardStatus>> loadStatuses() async {
    final json = await _getJsonList('/statuses');
    return [
      for (final item in json)
        BoardStatus(id: item['id'] as String, name: item['name'] as String, order: item['order'] as int),
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
  }) async {
    final response = await _client.put(
      _uri('/work-items/$id/details'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({
        'title': title,
        'description': description,
        'layerId': layerId,
        'priority': priority.toWire(),
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
  Future<WorkItemDetail> reschedule(String id, DateTime? startDate, DateTime? endDate) async {
    final response = await _client.post(
      _uri('/work-items/$id/schedule'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({
        'startDate': startDate?.toUtc().toIso8601String(),
        'endDate': endDate?.toUtc().toIso8601String(),
      }),
    );
    _checkOk(response, 'Failed to reschedule work item');
    return _toDetail(jsonDecode(response.body) as Map<String, dynamic>);
  }

  WorkItemDetail _toDetail(Map<String, dynamic> json) => WorkItemDetail(
    id: json['id'] as String,
    parentId: json['parentId'] as String?,
    title: json['title'] as String,
    description: json['description'] as String?,
    statusId: json['statusId'] as String,
    layerId: json['layerId'] as String?,
    priority: workItemPriorityFromWire(json['priority'] as String),
    assignedToUserId: json['assignedToUserId'] as String?,
    startDate: _parseDate(json['startDate']),
    endDate: _parseDate(json['endDate']),
    createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
    updatedAtUtc: DateTime.parse(json['updatedAtUtc'] as String),
  );

  DateTime? _parseDate(dynamic value) => value == null ? null : DateTime.parse(value as String);

  Future<List<Map<String, dynamic>>> _getJsonList(String path) async {
    final response = await _client.get(_uri(path));
    _checkOk(response, 'Failed to load $path');
    return (jsonDecode(response.body) as List<dynamic>).cast<Map<String, dynamic>>();
  }

  Uri _uri(String path) => Uri.parse('$_baseUrl$path');

  void _checkOk(http.Response response, String fallbackMessage) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    throw ApiException('${_problemDetail(response) ?? fallbackMessage} (${response.statusCode}).');
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
