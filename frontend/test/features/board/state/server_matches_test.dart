import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/state/server_matches.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';

import '../../../shared/fakes/work_items.dart';

void main() {
  final query = WorkItemQuery(spaceIds: {'home'}, text: 'paint');
  late List<Completer<List<WorkItem>>> answers;
  late ServerMatches matches;

  setUp(() {
    answers = [];
    matches = ServerMatches((_) {
      final answer = Completer<List<WorkItem>>();
      answers.add(answer);
      return answer.future;
    });
  });

  test('holds no answer until one arrives', () async {
    final asked = matches.refresh(query);
    expect(matches.ids, isNull);

    answers.single.complete([workItem('a'), workItem('b')]);

    expect(await asked, isTrue);
    expect(matches.ids, {'a', 'b'});
  });

  test('the same answer again is no change', () async {
    final first = matches.refresh(query);
    answers[0].complete([workItem('a')]);
    await first;

    final second = matches.refresh(query);
    answers[1].complete([workItem('a')]);

    expect(await second, isFalse);
  });

  test('a slower answer to an older question is dropped', () async {
    final older = matches.refresh(query);
    final newer = matches.refresh(query);

    answers[1].complete([workItem('new')]);
    answers[0].complete([workItem('old')]);

    expect(await newer, isTrue);
    expect(await older, isFalse);
    expect(matches.ids, {'new'});
  });

  test('settling supersedes a question still on its way', () async {
    final asked = matches.refresh(query);

    expect(matches.settle(const {}), isTrue);
    answers.single.complete([workItem('late')]);

    expect(await asked, isFalse);
    expect(matches.ids, isEmpty);
  });

  test('a failure is rethrown and keeps the last answer', () async {
    final first = matches.refresh(query);
    answers[0].complete([workItem('a')]);
    await first;

    final failed = matches.refresh(query);
    answers[1].completeError(const ApiException('down'));

    await expectLater(failed, throwsA(isA<ApiException>()));
    expect(matches.ids, {'a'});
  });

  test('a failure to a superseded question is swallowed', () async {
    final older = matches.refresh(query);
    matches.settle(null);

    answers.single.completeError(const ApiException('down'));

    expect(await older, isFalse);
  });
}
