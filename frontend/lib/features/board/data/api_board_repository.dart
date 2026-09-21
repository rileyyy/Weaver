import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

/// Board data backed by the REST API. Swimlanes are the direct children of
/// whichever scope item [loadBoard] is asked for (the first board's scope
/// item, or top-level items, by default); each swimlane's cards are that
/// swimlane's own direct children — mirroring `Board`'s doc comment on the
/// backend.
@LazySingleton(as: BoardRepository)
class ApiBoardRepository implements BoardRepository {
  ApiBoardRepository(this._client, @Named('apiBaseUrl') this._baseUrl);

  final http.Client _client;
  final String _baseUrl;

  @override
  Future<String?> loadRootScopeItemId() async {
    final boards = await _getJsonList('/boards');
    if (boards.isEmpty) return null;
    return boards.first['scopeItemId'] as String?;
  }

  @override
  Future<BoardData> loadBoard(String? scopeItemId) async {
    final statuses = await _loadStatuses();
    final swimlaneItems = await _loadChildren(scopeItemId);

    final swimlanes = await Future.wait([
      for (final parent in swimlaneItems) _loadSwimlane(parent),
    ]);

    return BoardData(statuses: statuses, swimlanes: swimlanes);
  }

  @override
  Future<List<HierarchyItem>> loadAllItems() async {
    final json = await _getJsonList('/work-items/all');
    return [for (final item in json) _toHierarchyItem(item)];
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
  Future<void> changeStatus(String cardId, String newStatusId) async {
    final response = await _client.post(
      _uri('/work-items/$cardId/status'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'statusId': newStatusId}),
    );
    _checkOk(response, 'Failed to change status');
  }

  @override
  Future<void> reparentItem(String itemId, String newParentId) async {
    final response = await _client.post(
      _uri('/work-items/$itemId/parent'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'parentId': newParentId}),
    );
    _checkOk(response, 'Failed to move item');
  }

  @override
  Future<void> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate,
  ) async {
    final response = await _client.post(
      _uri('/work-items/$itemId/schedule'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({
        'startDate': startDate?.toUtc().toIso8601String(),
        'endDate': endDate?.toUtc().toIso8601String(),
      }),
    );
    _checkOk(response, 'Failed to reschedule item');
  }

  @override
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String? parentId,
    required String statusId,
  }) async {
    final response = await _client.post(
      _uri('/work-items'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({
        'title': title,
        'description': description,
        'parentId': parentId,
        'statusId': statusId,
      }),
    );
    _checkOk(response, 'Failed to create work item');
  }

  @override
  Future<void> assign(String workItemId, String? userId) async {
    final response = await _client.post(
      _uri('/work-items/$workItemId/assignee'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'userId': userId}),
    );
    _checkOk(response, 'Failed to assign work item');
  }

  @override
  Future<void> setTags(String workItemId, List<String> tags) async {
    final response = await _client.post(
      _uri('/work-items/$workItemId/tags'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'tags': tags}),
    );
    _checkOk(response, 'Failed to set tags');
  }

  Future<List<BoardStatus>> _loadStatuses() async {
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

  Future<Swimlane> _loadSwimlane(Map<String, dynamic> parent) async {
    final parentId = parent['id'] as String;
    final cardItems = await _loadChildren(parentId);
    return Swimlane(
      parentId: parentId,
      title: parent['title'] as String,
      cards: [for (final item in cardItems) _toCard(item)],
      assignedToUserId: parent['assignedToUserId'] as String?,
    );
  }

  List<String> _toTags(Map<String, dynamic> item) =>
      (item['tags'] as List<dynamic>?)?.cast<String>() ?? const [];

  Future<List<Map<String, dynamic>>> _loadChildren(String? parentId) =>
      _getJsonList(
        '/work-items',
        parentId == null ? null : {'parentId': parentId},
      );

  WorkItemCard _toCard(Map<String, dynamic> item) => WorkItemCard(
    id: item['id'] as String,
    number: item['number'] as int,
    title: item['title'] as String,
    parentId: item['parentId'] as String,
    statusId: item['statusId'] as String,
    description: item['description'] as String?,
    startDate: _parseDate(item['startDate']),
    endDate: _parseDate(item['endDate']),
    assignedToUserId: item['assignedToUserId'] as String?,
    tags: _toTags(item),
  );

  HierarchyItem _toHierarchyItem(Map<String, dynamic> item) => HierarchyItem(
    id: item['id'] as String,
    number: item['number'] as int,
    parentId: item['parentId'] as String?,
    title: item['title'] as String,
    statusId: item['statusId'] as String,
    description: item['description'] as String?,
    startDate: _parseDate(item['startDate']),
    endDate: _parseDate(item['endDate']),
    assignedToUserId: item['assignedToUserId'] as String?,
    tags: _toTags(item),
  );

  DateTime? _parseDate(dynamic value) =>
      value == null ? null : DateTime.parse(value as String);

  Future<List<Map<String, dynamic>>> _getJsonList(
    String path, [
    Map<String, String>? query,
  ]) async {
    final response = await _client.get(_uri(path, query));
    _checkOk(response, 'Failed to load $path');
    final decoded = jsonDecode(response.body) as List<dynamic>;
    return decoded.cast<Map<String, dynamic>>();
  }

  Uri _uri(String path, [Map<String, String>? query]) {
    final uri = Uri.parse('$_baseUrl$path');
    return query == null ? uri : uri.replace(queryParameters: query);
  }

  void _checkOk(http.Response response, String fallbackMessage) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    throw ApiException(
      '${_problemDetail(response) ?? fallbackMessage} (${response.statusCode}).',
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
