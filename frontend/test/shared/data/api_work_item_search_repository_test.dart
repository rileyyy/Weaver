import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/shared/data/api_work_item_search_repository.dart';

import '../../core/network/fakes/fixed_api_endpoint.dart';

const _endpoint = FixedApiEndpoint('http://backend.test/api');

Map<String, dynamic> _itemJson(String id, int number) => {
  'id': id,
  'number': number,
  'spaceId': 'space-home',
  'parentId': null,
  'title': 'Item $number',
  'description': null,
  'statusId': 'status-todo',
  'typeId': null,
  'priorityId': null,
  'assignedToUserId': null,
  'tags': <String>[],
  'fields': <String, Object?>{},
  'rank': 0,
  'startDate': null,
  'endDate': null,
  'createdAtUtc': '2026-01-01T00:00:00Z',
  'updatedAtUtc': '2026-01-01T00:00:00Z',
  'version': 1,
};

void main() {
  test('searches one Space by text and parses the first page', () async {
    http.Request? sent;
    final client = MockClient((request) async {
      sent = request;
      return http.Response(
        jsonEncode({
          'items': [_itemJson('a', 3), _itemJson('b', 8)],
          'nextCursor': 'more',
        }),
        200,
      );
    });

    final items = await ApiWorkItemSearchRepository(
      client,
      _endpoint,
    ).search(spaceId: 'space-home', text: 'kitchen');

    expect(sent!.url.path, '/api/work-items/search');
    expect(sent!.url.queryParameters, {
      'spaceIds': 'space-home',
      'text': 'kitchen',
      'limit': '${ApiWorkItemSearchRepository.pageSize}',
    });
    expect(items.map((i) => i.number), [3, 8]);
  });

  test('leaves out blank text and a missing Space', () async {
    http.Request? sent;
    final client = MockClient((request) async {
      sent = request;
      return http.Response(jsonEncode({'items': [], 'nextCursor': null}), 200);
    });

    await ApiWorkItemSearchRepository(client, _endpoint).search(text: '  ');

    expect(sent!.url.queryParameters.keys, ['limit']);
  });
}
