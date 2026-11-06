import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/data/api_trash_repository.dart';
import 'package:weaver/shared/models/user_kind.dart';

import '../../../core/network/fakes/fixed_api_endpoint.dart';

Map<String, dynamic> _trashedJson({
  String? deletedByName = 'claude',
  String? deletedByKind = 'Agent',
}) => {
  'item': {
    'id': 'item-1',
    'number': 12,
    'title': 'Old shed',
    'spaceId': 'space-1',
    'spaceName': 'Home',
    'statusName': 'To Do',
    'typeName': null,
    'startDate': null,
    'endDate': null,
    'assigneeName': null,
    'childCount': 0,
    'trashedAtUtc': '2026-11-04T09:00:00Z',
  },
  'trashedAtUtc': '2026-11-04T09:00:00Z',
  'itemCount': 3,
  'deletedByName': deletedByName,
  'deletedByKind': deletedByKind,
  'deletedByActorLabel': 'tidy-up',
};

http.Response _json(Object body) => http.Response(
  jsonEncode(body),
  200,
  headers: {'content-type': 'application/json'},
);

void main() {
  const endpoint = FixedApiEndpoint('http://host/api');

  test('list reads each delete in the Trash', () async {
    final repository = ApiTrashRepository(
      MockClient((request) async {
        expect(request.method, 'GET');
        expect(request.url.path, '/api/trash');
        return _json([_trashedJson()]);
      }),
      endpoint,
    );

    final trash = await repository.list();

    final item = trash.single;
    expect(item.workItemId, 'item-1');
    expect(item.number, 12);
    expect(item.title, 'Old shed');
    expect(item.spaceName, 'Home');
    expect(item.trashedAt, DateTime.utc(2026, 11, 4, 9).toLocal());
    expect(item.itemCount, 3);
    expect(item.deletedByName, 'claude');
    expect(item.deletedByKind, UserKind.agent);
    expect(item.deletedByActorLabel, 'tidy-up');
  });

  test('list reads a delete whose deleter history no longer names', () async {
    final repository = ApiTrashRepository(
      MockClient(
        (_) async =>
            _json([_trashedJson(deletedByName: null, deletedByKind: null)]),
      ),
      endpoint,
    );

    final item = (await repository.list()).single;

    expect(item.deletedByName, isNull);
    expect(item.deletedByKind, isNull);
  });

  test("restore posts to the item's restore endpoint", () async {
    late http.Request sent;
    final repository = ApiTrashRepository(
      MockClient((request) async {
        sent = request;
        return _json({'id': 'item-1'});
      }),
      endpoint,
    );

    await repository.restore('item-1');

    expect(sent.method, 'POST');
    expect(sent.url.path, '/api/trash/item-1/restore');
  });

  test("a failed restore surfaces the server's status", () async {
    final repository = ApiTrashRepository(
      MockClient(
        (_) async => http.Response(
          jsonEncode({'detail': 'Restore its parent instead.'}),
          400,
          headers: {'content-type': 'application/problem+json'},
        ),
      ),
      endpoint,
    );

    await expectLater(
      () => repository.restore('item-1'),
      throwsA(
        isA<ApiException>().having((e) => e.statusCode, 'statusCode', 400),
      ),
    );
  });

  test('purge deletes the item from the Trash', () async {
    late http.Request sent;
    final repository = ApiTrashRepository(
      MockClient((request) async {
        sent = request;
        return http.Response('', 204);
      }),
      endpoint,
    );

    await repository.purge('item-1');

    expect(sent.method, 'DELETE');
    expect(sent.url.path, '/api/trash/item-1');
  });

  test("a refused purge surfaces the server's message", () async {
    final repository = ApiTrashRepository(
      MockClient(
        (_) async => http.Response(
          jsonEncode({'detail': 'Purge its parent instead.'}),
          400,
          headers: {'content-type': 'application/problem+json'},
        ),
      ),
      endpoint,
    );

    await expectLater(
      () => repository.purge('item-1'),
      throwsA(
        isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 400)
            .having((e) => e.message, 'message', contains('Purge its parent')),
      ),
    );
  });

  test('empty deletes the whole Trash', () async {
    late http.Request sent;
    final repository = ApiTrashRepository(
      MockClient((request) async {
        sent = request;
        return http.Response('', 204);
      }),
      endpoint,
    );

    await repository.empty();

    expect(sent.method, 'DELETE');
    expect(sent.url.path, '/api/trash');
  });
}
