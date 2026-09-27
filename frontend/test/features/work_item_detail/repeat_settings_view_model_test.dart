import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/repeat_settings_view_model.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';

import '../../shared/recurrence/fake_recurrence_repository.dart';

void main() {
  late FakeRecurrenceRepository repository;
  late RepeatSettingsViewModel viewModel;

  setUp(() {
    repository = FakeRecurrenceRepository();
    viewModel = RepeatSettingsViewModel(repository);
  });

  tearDown(() => viewModel.dispose());

  group('load', () {
    test('an item that does not repeat has no draft', () async {
      await viewModel.load(workItemId: 'item-1');

      expect(viewModel.isLoading, isFalse);
      expect(viewModel.saved, isNull);
      expect(viewModel.draft, isNull);
      expect(viewModel.isDirty, isFalse);
    });

    test(
      'a repeating item starts with its saved schedule, not dirty',
      () async {
        repository.schedules['item-1'] = recurrence();

        await viewModel.load(workItemId: 'item-1');

        expect(viewModel.saved, isNotNull);
        expect(viewModel.draft!.weekdays, {DateTime.monday});
        expect(viewModel.isDirty, isFalse);
      },
    );

    test('an occurrence loads its source repetition instead', () async {
      repository.schedules['template'] = recurrence(workItemId: 'template');

      await viewModel.load(workItemId: 'copy', recurrenceSourceId: 'template');

      expect(viewModel.isOccurrence, isTrue);
      expect(viewModel.source!.workItemId, 'template');
      expect(viewModel.draft, isNull);
    });

    test('a failed load reports an error', () async {
      repository.error = const ApiException('boom', statusCode: 500);

      await viewModel.load(workItemId: 'item-1');

      expect(viewModel.loadError, isNotNull);
      expect(viewModel.isLoading, isFalse);
    });
  });

  group('startRepeating', () {
    setUp(() => viewModel.load(workItemId: 'item-1'));

    test('defaults to weekly on the item start date', () {
      viewModel.startRepeating(itemStartDate: DateTime(2026, 10, 1, 15));

      final draft = viewModel.draft!;
      expect(draft.frequency, RecurrenceFrequency.weekly);
      expect(draft.startDate, DateTime(2026, 10, 1));
      expect(draft.weekdays, {DateTime.thursday});
      expect(viewModel.isDirty, isTrue);
      expect(viewModel.canSave, isTrue);
    });

    test('falls back to today without an item start date', () {
      viewModel.startRepeating(today: DateTime(2026, 9, 26));

      expect(viewModel.draft!.startDate, DateTime(2026, 9, 26));
    });
  });

  test('an invalid draft cannot be saved and is not sent', () async {
    await viewModel.load(workItemId: 'item-1');
    viewModel
      ..startRepeating(today: DateTime(2026, 9, 28))
      ..updateDraft(viewModel.draft!.toggleWeekday(DateTime.monday));

    expect(viewModel.canSave, isFalse);
    expect(await viewModel.save(), isFalse);
    expect(viewModel.saveError, contains('at least one day'));
    expect(repository.saveCalls, isEmpty);
  });

  test('save stores the schedule and reports a change', () async {
    await viewModel.load(workItemId: 'item-1');
    viewModel.startRepeating(today: DateTime(2026, 9, 28));

    final ok = await viewModel.save();

    expect(ok, isTrue);
    expect(repository.saveCalls.single.$1, 'item-1');
    expect(viewModel.saved, isNotNull);
    expect(viewModel.isDirty, isFalse);
    expect(viewModel.hasChanges, isTrue);
  });

  test(
    'a rejected save shows the server message and keeps the draft',
    () async {
      await viewModel.load(workItemId: 'item-1');
      viewModel.startRepeating(today: DateTime(2026, 9, 28));
      repository.error = const ApiException(
        'This item was created by a repeating item.',
        statusCode: 400,
      );

      final ok = await viewModel.save();

      expect(ok, isFalse);
      expect(viewModel.saveError, 'This item was created by a repeating item.');
      expect(viewModel.isDirty, isTrue);
      expect(viewModel.hasChanges, isFalse);
    },
  );

  test('a failed save for another reason shows a generic message', () async {
    await viewModel.load(workItemId: 'item-1');
    viewModel.startRepeating(today: DateTime(2026, 9, 28));
    repository.error = const ApiException('down', statusCode: 503);

    await viewModel.save();

    expect(
      viewModel.saveError,
      'Could not save the repeat settings. Try again.',
    );
  });

  test('discardChanges goes back to the saved schedule', () async {
    repository.schedules['item-1'] = recurrence();
    await viewModel.load(workItemId: 'item-1');
    viewModel.updateDraft(
      viewModel.draft!.withFrequency(RecurrenceFrequency.yearly),
    );
    expect(viewModel.isDirty, isTrue);

    viewModel.discardChanges();

    expect(viewModel.draft!.frequency, RecurrenceFrequency.weekly);
    expect(viewModel.isDirty, isFalse);
  });

  test('discardChanges on an unsaved new draft removes it', () async {
    await viewModel.load(workItemId: 'item-1');
    viewModel
      ..startRepeating(today: DateTime(2026, 9, 28))
      ..discardChanges();

    expect(viewModel.draft, isNull);
  });

  test('stopRepeating removes the schedule', () async {
    repository.schedules['item-1'] = recurrence();
    await viewModel.load(workItemId: 'item-1');

    final ok = await viewModel.stopRepeating();

    expect(ok, isTrue);
    expect(repository.removeCalls, ['item-1']);
    expect(viewModel.saved, isNull);
    expect(viewModel.draft, isNull);
    expect(viewModel.hasChanges, isTrue);
  });

  test('a failed stop keeps the schedule', () async {
    repository.schedules['item-1'] = recurrence();
    await viewModel.load(workItemId: 'item-1');
    repository.error = const ApiException('down', statusCode: 503);

    final ok = await viewModel.stopRepeating();

    expect(ok, isFalse);
    expect(viewModel.saved, isNotNull);
    expect(viewModel.saveError, isNotNull);
  });
}
