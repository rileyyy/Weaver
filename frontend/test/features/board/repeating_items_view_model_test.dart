import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/repeating_items_view_model.dart';
import 'package:weaver/shared/recurrence/data/recurrence_repository.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';
import 'package:weaver/shared/recurrence/models/work_item_recurrence.dart';

import '../../shared/recurrence/fake_recurrence_repository.dart';

/// Lets a test finish each `list()` call when it chooses.
class _ControlledListRepository extends FakeRecurrenceRepository
    implements RecurrenceRepository {
  final List<Completer<List<WorkItemRecurrence>>> pending = [];

  @override
  Future<List<WorkItemRecurrence>> list() {
    final completer = Completer<List<WorkItemRecurrence>>();
    pending.add(completer);
    return completer.future;
  }
}

void main() {
  late FakeRecurrenceRepository repository;
  late RepeatingItemsViewModel viewModel;

  setUp(() {
    repository = FakeRecurrenceRepository([
      recurrence(workItemId: 'a', number: 1),
      recurrence(workItemId: 'b', number: 2),
    ]);
    viewModel = RepeatingItemsViewModel(repository);
  });

  tearDown(() => viewModel.dispose());

  test('load lists every repeating item', () async {
    await viewModel.load();

    expect(viewModel.items.map((r) => r.workItemId), ['a', 'b']);
    expect(viewModel.isLoading, isFalse);
    expect(viewModel.loadError, isNull);
  });

  test('a failed load reports an error', () async {
    repository.error = const ApiException('down', statusCode: 503);

    await viewModel.load();

    expect(viewModel.loadError, isNotNull);
  });

  test('a slower earlier load cannot overwrite a newer one', () async {
    final controlled = _ControlledListRepository();
    final vm = RepeatingItemsViewModel(controlled);
    addTearDown(vm.dispose);

    final first = vm.load();
    final second = vm.load();
    controlled.pending[1].complete([recurrence(workItemId: 'new')]);
    await second;
    controlled.pending[0].complete([recurrence(workItemId: 'old')]);
    await first;

    expect(vm.items.single.workItemId, 'new');
  });

  test('isLoading only shows for the first load', () async {
    await viewModel.load();

    final reload = viewModel.load();
    expect(viewModel.isLoading, isFalse);
    await reload;
  });

  test('save replaces that item with the server response', () async {
    await viewModel.load();

    final error = await viewModel.save(
      'b',
      RecurrenceDraft(
        frequency: RecurrenceFrequency.monthly,
        weekdays: const {},
        startDate: DateTime(2026, 10, 15),
        endDate: DateTime(2027, 3, 15),
      ),
    );

    expect(error, isNull);
    final saved = viewModel.items.singleWhere((r) => r.workItemId == 'b');
    expect(saved.frequency, RecurrenceFrequency.monthly);
    expect(saved.endDate, DateTime(2027, 3, 15));
    expect(viewModel.items.map((r) => r.workItemId), ['a', 'b']);
  });

  test('save rejects an invalid draft without a request', () async {
    await viewModel.load();

    final error = await viewModel.save(
      'a',
      RecurrenceDraft(
        frequency: RecurrenceFrequency.weekly,
        weekdays: const {},
        startDate: DateTime(2026, 10, 15),
        endDate: null,
      ),
    );

    expect(error, contains('at least one day'));
    expect(repository.saveCalls, isEmpty);
  });

  test('save returns the server message for a 400', () async {
    await viewModel.load();
    repository.error = const ApiException('Bad schedule.', statusCode: 400);

    final error = await viewModel.save(
      'a',
      RecurrenceDraft.startingOn(DateTime(2026, 10, 1)),
    );

    expect(error, 'Bad schedule.');
  });

  test('stopRepeating removes the item from the list', () async {
    await viewModel.load();

    final ok = await viewModel.stopRepeating('a');

    expect(ok, isTrue);
    expect(viewModel.items.map((r) => r.workItemId), ['b']);
  });

  test('a failed stop keeps the item and reports why', () async {
    await viewModel.load();
    repository.error = const ApiException('down', statusCode: 503);

    final ok = await viewModel.stopRepeating('a');

    expect(ok, isFalse);
    expect(viewModel.items, hasLength(2));
    expect(viewModel.actionError, isNotNull);
    viewModel.clearActionError();
    expect(viewModel.actionError, isNull);
  });
}
