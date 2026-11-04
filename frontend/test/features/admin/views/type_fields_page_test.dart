import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/custom_field_admin_view_model.dart';
import 'package:weaver/features/admin/views/type_fields_page.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/custom_field_option.dart';

import '../fakes/fake_custom_field_admin_repository.dart';

void main() {
  late FakeCustomFieldAdminRepository repository;

  Future<void> pump(WidgetTester tester) async {
    repository = FakeCustomFieldAdminRepository(const [
      CustomField(
        id: 'field-budget',
        typeId: 'type-room',
        name: 'Budget',
        kind: CustomFieldKind.currency,
        order: 0,
      ),
      CustomField(
        id: 'field-floor',
        typeId: 'type-room',
        name: 'Floor',
        kind: CustomFieldKind.singleSelect,
        order: 1,
        options: [
          CustomFieldOption(id: 'option-oak', name: 'Oak'),
          CustomFieldOption(id: 'option-tiles', name: 'Tiles'),
        ],
      ),
    ]);
    await tester.pumpWidget(
      MaterialApp(
        home: TypeFieldsPage(
          typeId: 'type-room',
          typeName: 'Room',
          viewModel: CustomFieldAdminViewModel(repository, 'type-room'),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('lists the fields with their kind and options', (tester) async {
    await pump(tester);

    expect(find.text('Room fields'), findsOneWidget);
    expect(find.text('Currency'), findsOneWidget);
    expect(find.text('Single select · Oak, Tiles'), findsOneWidget);
  });

  testWidgets('removing a field asks first, then removes it', (tester) async {
    await pump(tester);

    await tester.tap(find.byTooltip('Delete Budget'));
    await tester.pumpAndSettle();
    expect(find.textContaining('loses its Budget value'), findsOneWidget);
    await tester.tap(find.text('Remove'));
    await tester.pumpAndSettle();

    expect(repository.calls.last, 'delete field-budget');
    expect(find.text('Budget'), findsNothing);
  });

  testWidgets('adding a field appends it', (tester) async {
    await pump(tester);

    await tester.tap(find.text('Add field'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Name'), 'Due');
    await tester.pump();
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();

    expect(repository.calls.last, 'create Due Text [] in type-room');
    expect(find.text('Due'), findsOneWidget);
  });
}
