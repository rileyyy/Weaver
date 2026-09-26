import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/shared/recurrence/data/api_recurrence_repository.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';

Map<String, dynamic> _recurrenceJson({String workItemId = 'item-1'}) => {
  'workItemId': workItemId,
  'workItemNumber': 3,
  'workItemTitle': 'Report',
  'parentId': null,
  'parentTitle': null,
  'frequency': 'Weekly',
  'daysOfWeek': ['Friday'],
  'startDate': '2026-09-25',
  'endDate': null,
  'nextOccurrence': '2026-10-02',
};

http.Response _json(Object body) => http.Response(
  jsonEncode(body),
  200,
  headers: {'content-type': 'application/json'},
);

void main() {
  test('get returns null when the item does not repeat (204)', () async {
    final repository = ApiRecurrenceRepository(
      MockClient((request) async {
        expect(request.url.path, '/api/work-items/item-1/recurrence');
        return http.Response('', 204);
      }),
      'http://host/api',
    );

    expect(await repository.get('item-1'), isNull);
  });

  test('get parses a schedule', () async {
    final repository = ApiRecurrenceRepository(
      MockClient((_) async => _json(_recurrenceJson())),
      'http://host/api',
    );

    final result = await repository.get('item-1');

    expect(result!.weekdays, {DateTime.friday});
  });

  test('list parses every schedule', () async {
    final repository = ApiRecurrenceRepository(
      MockClient((request) async {
        expect(request.url.path, '/api/recurrences');
        return _json([
          _recurrenceJson(),
          _recurrenceJson(workItemId: 'item-2'),
        ]);
      }),
      'http://host/api',
    );

    final result = await repository.list();

    expect(result.map((r) => r.workItemId), ['item-1', 'item-2']);
  });

  test('save posts the draft', () async {
    late http.Request sent;
    final repository = ApiRecurrenceRepository(
      MockClient((request) async {
        sent = request;
        return _json(_recurrenceJson());
      }),
      'http://host/api',
    );

    await repository.save(
      'item-1',
      RecurrenceDraft(
        frequency: RecurrenceFrequency.weekly,
        weekdays: const {DateTime.friday},
        startDate: DateTime(2026, 9, 25),
        endDate: null,
      ),
    );

    expect(sent.method, 'POST');
    expect(sent.url.path, '/api/work-items/item-1/recurrence');
    expect(jsonDecode(sent.body), {
      'frequency': 'Weekly',
      'daysOfWeek': ['Friday'],
      'startDate': '2026-09-25',
      'endDate': null,
    });
  });

  test('save surfaces the server validation message', () async {
    final repository = ApiRecurrenceRepository(
      MockClient(
        (_) async => http.Response(
          jsonEncode({'detail': 'Choose at least one day of the week.'}),
          400,
        ),
      ),
      'http://host/api',
    );

    await expectLater(
      repository.save(
        'item-1',
        RecurrenceDraft.startingOn(DateTime(2026, 9, 25)),
      ),
      throwsA(
        isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 400)
            .having((e) => e.message, 'message', contains('Choose at least')),
      ),
    );
  });

  test('remove sends a DELETE', () async {
    late http.Request sent;
    final repository = ApiRecurrenceRepository(
      MockClient((request) async {
        sent = request;
        return http.Response('', 204);
      }),
      'http://host/api',
    );

    await repository.remove('item-1');

    expect(sent.method, 'DELETE');
    expect(sent.url.path, '/api/work-items/item-1/recurrence');
  });
}
