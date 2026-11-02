import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/data/api_work_item_history_repository.dart';
import '../../../core/network/fakes/fixed_api_endpoint.dart';

void main() {
  test('loadHistory reads the item history endpoint', () async {
    final repository = ApiWorkItemHistoryRepository(
      MockClient((request) async {
        expect(request.method, 'GET');
        expect(request.url.path, '/api/work-items/item-1/history');
        return http.Response(
          jsonEncode([
            {
              'id': 2,
              'workItemId': 'item-1',
              'changedAtUtc': '2026-09-01T12:00:00Z',
              'accountName': 'alice',
              'accountKind': 'Human',
              'field': 'title',
              'oldValue': 'Task',
              'newValue': 'Renamed',
            },
          ]),
          200,
          headers: {'content-type': 'application/json'},
        );
      }),
      const FixedApiEndpoint('http://host/api'),
    );

    final history = await repository.loadHistory('item-1');

    expect(history.single.change, 'Title: Task → Renamed');
  });

  test('loadHistory turns a failure into an ApiException', () {
    final repository = ApiWorkItemHistoryRepository(
      MockClient((_) async => http.Response('', 500)),
      const FixedApiEndpoint('http://host/api'),
    );

    expect(repository.loadHistory('item-1'), throwsA(isA<ApiException>()));
  });
}
