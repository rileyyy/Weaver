import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/widgets/custom_fields_section.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/roll_up.dart';

const _budget = CustomField(
  id: 'field-budget',
  typeId: 'type-room',
  name: 'Budget',
  kind: CustomFieldKind.currency,
  order: 0,
  rollsUp: true,
);

const _hours = CustomField(
  id: 'field-hours',
  typeId: 'type-room',
  name: 'Hours',
  kind: CustomFieldKind.number,
  order: 1,
  rollsUp: true,
);

void main() {
  testWidgets(
    "shows the sub-items' total under the field, apart from its own value",
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: CustomFieldsSection(
              fields: const [_budget, _hours],
              values: const {'field-budget': 1000},
              users: const [],
              rollUpFor: (fieldId) => fieldId == 'field-budget'
                  ? const RollUp(
                      fieldId: 'field-budget',
                      fieldName: 'Budget',
                      typeId: 'type-room',
                      kind: CustomFieldKind.currency,
                      total: 450.5,
                    )
                  : null,
              onSave: (_, _) async => true,
            ),
          ),
        ),
      );

      expect(find.widgetWithText(TextField, '1000.00'), findsOneWidget);
      expect(find.text('Sub-items total: 450.50'), findsOneWidget);
      expect(find.textContaining('Sub-items total'), findsOneWidget);
    },
  );
}
