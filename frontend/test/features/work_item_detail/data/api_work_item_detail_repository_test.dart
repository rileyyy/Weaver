import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/features/work_item_detail/data/api_work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';

http.Response _jsonResponse(Object body, {int statusCode = 200}) =>
    http.Response(jsonEncode(body), statusCode, headers: {'content-type': 'application/json'});

Map<String, dynamic> _itemJson({
  String? layerId,
  String priority = 'Medium',
  String? assignedToUserId,
}) => {
  'id': 'item-1',
  'parentId': null,
  'title': 'A task',
  'description': null,
  'statusId': 'status-todo',
  'layerId': layerId,
  'priority': priority,
  'assignedToUserId': assignedToUserId,
  'rank': 0,
  'startDate': null,
  'endDate': null,
  'createdAtUtc': '2026-01-01T00:00:00Z',
  'updatedAtUtc': '2026-01-01T00:00:00Z',
};

void main() {
  const baseUrl = 'http://backend.test/api';

  test('getItem parses the work item, including layer/priority/assignee', () async {
    final client = MockClient((request) async => _jsonResponse(
          _itemJson(layerId: 'layer-1', priority: 'Urgent', assignedToUserId: 'user-1'),
        ));
    final repository = ApiWorkItemDetailRepository(client, baseUrl);

    final item = await repository.getItem('item-1');

    expect(item.layerId, 'layer-1');
    expect(item.priority, WorkItemPriority.urgent);
    expect(item.assignedToUserId, 'user-1');
  });

  test('loadLayers parses the layer list', () async {
    final client = MockClient((request) async => _jsonResponse([
          {'id': 'layer-1', 'name': 'Project', 'order': 0},
        ]));
    final repository = ApiWorkItemDetailRepository(client, baseUrl);

    final layers = await repository.loadLayers();

    expect(layers.single.name, 'Project');
  });

  test('loadUsers parses the user list', () async {
    final client = MockClient((request) async => _jsonResponse([
          {'id': 'user-1', 'username': 'alice', 'kind': 'Human'},
        ]));
    final repository = ApiWorkItemDetailRepository(client, baseUrl);

    final users = await repository.loadUsers();

    expect(users.single.username, 'alice');
  });

  test('updateDetails puts the new fields and parses the response', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_itemJson(layerId: 'layer-1', priority: 'High'));
    });
    final repository = ApiWorkItemDetailRepository(client, baseUrl);

    await repository.updateDetails(
      'item-1',
      title: 'New title',
      description: 'New description',
      layerId: 'layer-1',
      priority: WorkItemPriority.high,
    );

    expect(sentRequest!.method, 'PUT');
    expect(sentRequest!.url.path, '/api/work-items/item-1/details');
    expect(jsonDecode(sentRequest!.body), {
      'title': 'New title',
      'description': 'New description',
      'layerId': 'layer-1',
      'priority': 'High',
    });
  });

  test('assign posts the userId to the assignee endpoint', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_itemJson(assignedToUserId: 'user-1'));
    });
    final repository = ApiWorkItemDetailRepository(client, baseUrl);

    await repository.assign('item-1', 'user-1');

    expect(sentRequest!.url.path, '/api/work-items/item-1/assignee');
    expect(jsonDecode(sentRequest!.body), {'userId': 'user-1'});
  });

  test('reschedule posts the new dates', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_itemJson());
    });
    final repository = ApiWorkItemDetailRepository(client, baseUrl);
    final start = DateTime.utc(2026, 2, 1);

    await repository.reschedule('item-1', start, null);

    expect(sentRequest!.url.path, '/api/work-items/item-1/schedule');
    expect(jsonDecode(sentRequest!.body), {
      'startDate': start.toIso8601String(),
      'endDate': null,
    });
  });
}
