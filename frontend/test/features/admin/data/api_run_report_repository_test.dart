import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/features/admin/data/api_run_report_repository.dart';
import 'package:weaver/features/admin/models/run_report.dart';

import '../../../core/network/fakes/fixed_api_endpoint.dart';

const _endpoint = FixedApiEndpoint('http://backend.test/api');

Map<String, dynamic> _runJson({String? apiKeyId = 'k1'}) => {
  'accountId': 'a1',
  'accountName': 'Runner: llama / When',
  'apiKeyId': apiKeyId,
  'apiKeyLabel': apiKeyId == null ? null : 'nightly',
  'runId': 'run 1',
  'firstChangeAtUtc': '2026-11-10T02:00:00Z',
  'lastChangeAtUtc': '2026-11-10T02:05:00Z',
  'changeCount': 3,
};

Map<String, dynamic> _changedItemJson() => {
  'item': {'id': 'w1', 'number': 12, 'title': 'Paint the hall'},
  'deleted': true,
  'restored': false,
  'changes': [
    {
      'id': 7,
      'workItemId': 'w1',
      'changedAtUtc': '2026-11-10T02:00:00Z',
      'accountName': 'Runner: llama / When',
      'accountKind': 'Agent',
      'actorLabel': 'resolver',
      'runId': 'run 1',
      'field': 'when',
      'oldValue': null,
      'newValue': 'next week',
    },
  ],
};

void main() {
  late List<http.Request> requests;
  late ApiRunReportRepository repository;

  setUp(() {
    requests = [];
    final client = MockClient((request) async {
      requests.add(request);
      return request.url.path.endsWith('/changes')
          ? http.Response(jsonEncode([_changedItemJson()]), 200)
          : http.Response(
              jsonEncode({
                'runs': [_runJson()],
                'nextOffset': 20,
              }),
              200,
            );
    });
    repository = ApiRunReportRepository(client, _endpoint);
  });

  test('list asks for one page and reads its runs', () async {
    final page = await repository.list(offset: 20, limit: 10);

    final run = page.runs.single;
    expect(requests.single.url.path, '/api/admin/runs');
    expect(requests.single.url.queryParameters, {
      'offset': '20',
      'limit': '10',
    });
    expect(page.nextOffset, 20);
    expect(run.accountName, 'Runner: llama / When');
    expect(run.apiKeyLabel, 'nightly');
    expect(run.runId, 'run 1');
    expect(run.firstChangeAt, DateTime.utc(2026, 11, 10, 2).toLocal());
    expect(run.changeCount, 3);
  });

  test('changes names the run by account, key and run id', () async {
    final run = RunReport.fromJson(_runJson());

    final items = await repository.changes(run);

    expect(requests.single.url.path, '/api/admin/runs/changes');
    expect(requests.single.url.queryParameters, {
      'accountId': 'a1',
      'apiKeyId': 'k1',
      'runId': 'run 1',
    });
    final item = items.single;
    expect(item.number, 12);
    expect(item.title, 'Paint the hall');
    expect(item.isDeleted, isTrue);
    expect(item.changes.single.change, 'When: none → next week');
    expect(item.changes.single.actorLabel, 'resolver');
    expect(item.changes.single.runId, 'run 1');
  });

  test('changes leaves the key out for a run sent without one', () async {
    await repository.changes(RunReport.fromJson(_runJson(apiKeyId: null)));

    expect(
      requests.single.url.queryParameters.containsKey('apiKeyId'),
      isFalse,
    );
  });
}
