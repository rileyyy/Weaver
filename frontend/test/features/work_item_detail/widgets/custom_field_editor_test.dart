import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/widgets/custom_field_editor.dart';
import 'package:weaver/features/work_item_detail/widgets/custom_field_text_editor.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/custom_field_option.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/user_kind.dart';

CustomField _field(
  CustomFieldKind kind, {
  List<CustomFieldOption> options = const [],
}) => CustomField(
  id: 'field-1',
  typeId: 'type-room',
  name: 'Field',
  kind: kind,
  order: 0,
  options: options,
);

const _oak = CustomFieldOption(id: 'option-oak', name: 'Oak');
const _tiles = CustomFieldOption(id: 'option-tiles', name: 'Tiles');

void main() {
  late List<Object?> saved;

  setUp(() => saved = []);

  Future<void> pump(WidgetTester tester, CustomField field, {Object? value}) =>
      tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: CustomFieldEditor(
              field: field,
              value: value,
              users: const [
                User(id: 'user-1', username: 'alice', kind: UserKind.human),
              ],
              onSave: (value) async {
                saved.add(value);
                return true;
              },
            ),
          ),
        ),
      );

  group('text-like kinds', () {
    testWidgets('a text field saves its trimmed text on submit', (
      tester,
    ) async {
      await pump(tester, _field(CustomFieldKind.text));

      await tester.enterText(find.byType(TextField), '  Oak floor ');
      await tester.testTextInput.receiveAction(TextInputAction.done);

      expect(saved, ['Oak floor']);
    });

    testWidgets('a number field saves a number', (tester) async {
      await pump(tester, _field(CustomFieldKind.number), value: 3);

      expect(find.text('3'), findsOneWidget);
      await tester.enterText(find.byType(TextField), '12,5');
      await tester.testTextInput.receiveAction(TextInputAction.done);

      expect(saved, [12.5]);
    });

    testWidgets('a number field refuses a word without saving', (tester) async {
      await pump(tester, _field(CustomFieldKind.currency));

      await tester.enterText(find.byType(TextField), 'lots');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pump();

      expect(saved, isEmpty);
      expect(find.text('Enter a number.'), findsOneWidget);
    });

    testWidgets('clearing a field saves null', (tester) async {
      await pump(tester, _field(CustomFieldKind.url), value: 'https://a.test');

      await tester.enterText(find.byType(TextField), '');
      await tester.testTextInput.receiveAction(TextInputAction.done);

      expect(saved, [null]);
    });

    testWidgets('a Markdown field is multi-line', (tester) async {
      await pump(tester, _field(CustomFieldKind.markdown), value: '- one');

      expect(tester.widget<TextField>(find.byType(TextField)).maxLines, 8);
    });
  });

  test('currency shows two decimals, and a whole number none', () {
    expect(
      CustomFieldTextEditor.textOf(CustomFieldKind.currency, 19.9),
      '19.90',
    );
    expect(
      CustomFieldTextEditor.textOf(CustomFieldKind.number, 1200.0),
      '1200',
    );
  });

  test('a URL must be a web address', () {
    final parsed = CustomFieldTextEditor.parse(
      CustomFieldKind.url,
      'example.com',
    );

    expect(parsed.error, 'Enter an http or https address.');
  });

  testWidgets('a checkbox saves as soon as it is ticked', (tester) async {
    await pump(tester, _field(CustomFieldKind.checkbox));

    await tester.tap(find.byType(Checkbox));

    expect(saved, [true]);
  });

  testWidgets('a date field offers a picker and shows the stored date', (
    tester,
  ) async {
    await pump(tester, _field(CustomFieldKind.date), value: '2026-12-24');

    expect(find.byIcon(Icons.event_outlined), findsOneWidget);
    await tester.tap(find.byTooltip('Clear date'));

    expect(saved, [null]);
  });

  testWidgets('a single select saves the chosen option id', (tester) async {
    await pump(
      tester,
      _field(CustomFieldKind.singleSelect, options: [_oak, _tiles]),
    );

    await tester.tap(find.byType(DropdownButtonFormField<String?>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tiles').last);
    await tester.pumpAndSettle();

    expect(saved, ['option-tiles']);
  });

  testWidgets('a multi-select adds a choice in option order', (tester) async {
    await pump(
      tester,
      _field(CustomFieldKind.multiSelect, options: [_oak, _tiles]),
      value: ['option-tiles'],
    );

    await tester.tap(find.widgetWithText(FilterChip, 'Oak'));

    expect(saved, [
      ['option-oak', 'option-tiles'],
    ]);
  });

  testWidgets('a multi-select with its last choice removed saves null', (
    tester,
  ) async {
    await pump(
      tester,
      _field(CustomFieldKind.multiSelect, options: [_oak]),
      value: ['option-oak'],
    );

    await tester.tap(find.widgetWithText(FilterChip, 'Oak'));

    expect(saved, [null]);
  });

  testWidgets('a user field offers the users and saves an id', (tester) async {
    await pump(tester, _field(CustomFieldKind.user));

    await tester.tap(find.byType(DropdownButtonFormField<String?>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('alice').last);
    await tester.pumpAndSettle();

    expect(saved, ['user-1']);
  });
}
