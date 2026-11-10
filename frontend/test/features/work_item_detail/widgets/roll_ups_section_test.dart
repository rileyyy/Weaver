import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/widgets/roll_ups_section.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/roll_up.dart';

void main() {
  Future<void> pump(WidgetTester tester, List<RollUp> rollUps) =>
      tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: RollUpsSection(
              rollUps: rollUps,
              typeNameFor: (typeId) => typeId == 'type-task' ? 'Task' : null,
            ),
          ),
        ),
      );

  testWidgets("names each total's field and Type", (tester) async {
    await pump(tester, const [
      RollUp(
        fieldId: 'field-hours',
        fieldName: 'Hours',
        typeId: 'type-task',
        kind: CustomFieldKind.number,
        total: 7.5,
      ),
      RollUp(
        fieldId: 'field-cost',
        fieldName: 'Cost',
        typeId: 'type-gone',
        kind: CustomFieldKind.currency,
        total: 12,
      ),
    ]);

    expect(find.text('Totals of sub-items'), findsOneWidget);
    expect(find.text('Hours (Task): 7.5'), findsOneWidget);
    expect(find.text('Cost: 12.00'), findsOneWidget);
  });

  testWidgets('shows nothing without totals', (tester) async {
    await pump(tester, const []);

    expect(find.text('Totals of sub-items'), findsNothing);
  });
}
