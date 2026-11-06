import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/models/custom_field_option_draft.dart';
import 'package:weaver/features/admin/widgets/custom_field_dialog.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/custom_field_option.dart';

const _floor = CustomField(
  id: 'field-floor',
  typeId: 'type-room',
  name: 'Floor',
  kind: CustomFieldKind.singleSelect,
  order: 0,
  options: [
    CustomFieldOption(id: 'option-oak', name: 'Oak'),
    CustomFieldOption(id: 'option-tiles', name: 'Tiles'),
  ],
);

void main() {
  late List<(String, CustomFieldKind, List<CustomFieldOptionDraft>, bool)>
  saved;

  Future<void> open(WidgetTester tester, {CustomField? initial}) async {
    saved = [];
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => TextButton(
            onPressed: () => showCustomFieldDialog(
              context,
              initial: initial,
              onSave: (name, kind, options, {required rollsUp}) async {
                saved.add((name, kind, options, rollsUp));
                return null;
              },
            ),
            child: const Text('open'),
          ),
        ),
      ),
    );
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
  }

  testWidgets('adds a select field with the options typed', (tester) async {
    await open(tester);

    await tester.enterText(find.widgetWithText(TextField, 'Name'), 'Wood');
    await tester.tap(find.byType(DropdownButtonFormField<CustomFieldKind>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Multi-select').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Add option'));
    await tester.pump();
    await tester.enterText(find.widgetWithText(TextField, 'Option 1'), 'Oak');
    await tester.pump();
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();

    expect(saved.single.$1, 'Wood');
    expect(saved.single.$2, CustomFieldKind.multiSelect);
    expect(saved.single.$3.map((o) => o.name), ['Oak']);
  });

  testWidgets('a number field can be set to roll up', (tester) async {
    await open(tester);

    expect(find.text('Roll up into parents'), findsNothing);
    await tester.enterText(find.widgetWithText(TextField, 'Name'), 'Hours');
    await tester.tap(find.byType(DropdownButtonFormField<CustomFieldKind>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Number').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Roll up into parents'));
    await tester.pump();
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();

    expect(saved.single.$2, CustomFieldKind.number);
    expect(saved.single.$4, isTrue);
  });

  testWidgets('editing a field that rolls up keeps it on', (tester) async {
    await open(
      tester,
      initial: const CustomField(
        id: 'field-budget',
        typeId: 'type-room',
        name: 'Budget',
        kind: CustomFieldKind.currency,
        order: 0,
        rollsUp: true,
      ),
    );

    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();

    expect(saved.single.$4, isTrue);
  });

  testWidgets('a select field with a blank option cannot be saved', (
    tester,
  ) async {
    await open(tester, initial: _floor);

    await tester.tap(find.text('Add option'));
    await tester.pump();
    await tester.tap(find.text('Save'));
    await tester.pump();

    expect(saved, isEmpty);
  });

  testWidgets('editing keeps option ids, reorders and warns on removal', (
    tester,
  ) async {
    await open(tester, initial: _floor);

    await tester.tap(find.byTooltip('Move option up').last);
    await tester.pump();
    await tester.tap(find.byTooltip('Remove option').last);
    await tester.pump();

    expect(
      find.text('Items that chose a removed option lose that choice.'),
      findsOneWidget,
    );
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();

    expect(saved.single.$3.map((o) => o.id), ['option-tiles']);
  });

  testWidgets("an existing field's kind can't be changed", (tester) async {
    await open(tester, initial: _floor);

    final kind = tester.widget<DropdownButton<CustomFieldKind>>(
      find.byType(DropdownButton<CustomFieldKind>),
    );
    expect(kind.onChanged, isNull);
    expect(find.text('A field’s kind can’t be changed.'), findsOneWidget);
  });
}
