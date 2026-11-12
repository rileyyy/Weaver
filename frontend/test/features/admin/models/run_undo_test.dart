import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/models/run_undo_change.dart';
import 'package:weaver/features/admin/models/run_undo_outcome.dart';
import 'package:weaver/features/admin/models/run_undo_result.dart';

RunUndoChange _change(String field, RunUndoOutcome outcome, {int? number}) =>
    RunUndoChange(
      number: number,
      title: 'Paint the hall',
      field: field,
      outcome: outcome,
    );

void main() {
  test('an outcome a newer server sends reads as not undoable', () {
    expect(RunUndoOutcome.fromJson('Undone'), RunUndoOutcome.undone);
    expect(RunUndoOutcome.fromJson('SomethingNew'), RunUndoOutcome.notUndoable);
  });

  test('only changes left as they are have a reason', () {
    expect(RunUndoOutcome.undone.skipReason, isNull);
    expect(RunUndoOutcome.kept.skipReason, isNull);
    expect(RunUndoOutcome.invalidDates.skipReason, isNotNull);
  });

  test(
    'a result counts undone changes and kept comments, and lists the rest',
    () {
      final result = RunUndoResult([
        _change('startDate', RunUndoOutcome.undone),
        _change('endDate', RunUndoOutcome.undone),
        _change('comment', RunUndoOutcome.kept),
        _change('statusId', RunUndoOutcome.notUndoable),
      ]);

      expect(result.undoneCount, 2);
      expect(result.keptCount, 1);
      expect(result.skipped.map((c) => c.field), ['statusId']);
    },
  );

  test('names fields, history entries and custom fields', () {
    expect(
      _change('startDate', RunUndoOutcome.undone).fieldLabel,
      'Start date',
    );
    expect(_change('urlLink', RunUndoOutcome.undone).fieldLabel, 'URL link');
    expect(
      _change('fields.0c3e', RunUndoOutcome.notUndoable).fieldLabel,
      'Custom field',
    );
  });

  test('names the item by number, or by title once it is purged', () {
    expect(
      _change('when', RunUndoOutcome.undone, number: 12).itemName,
      '#12 Paint the hall',
    );
    expect(_change('when', RunUndoOutcome.undone).itemName, 'Paint the hall');
  });
}
