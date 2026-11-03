import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/header/widgets/space_switcher.dart';
import 'package:weaver/shared/models/space.dart';

const _home = Space(id: 'home', name: 'Home');
const _village = Space(id: 'village', name: 'Village');

Widget _switcher({
  List<Space> spaces = const [_home, _village],
  Space? current = _home,
  List<Space>? switched,
}) => MaterialApp(
  home: Scaffold(
    body: SpaceSwitcher(
      spaces: spaces,
      current: current,
      onSwitch: (space) async => switched?.add(space),
    ),
  ),
);

void main() {
  testWidgets('picking another Space switches to it', (tester) async {
    final switched = <Space>[];
    await tester.pumpWidget(_switcher(switched: switched));

    await tester.tap(find.byTooltip('Switch Space'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Village'));
    await tester.pumpAndSettle();

    expect(switched, [_village]);
  });

  testWidgets('the menu marks the current Space', (tester) async {
    await tester.pumpWidget(_switcher());

    await tester.tap(find.byTooltip('Switch Space'));
    await tester.pumpAndSettle();

    final homeItem = find.ancestor(
      of: find.text('Home'),
      matching: find.byType(PopupMenuItem<Space>),
    );
    expect(
      find.descendant(of: homeItem, matching: find.byIcon(Icons.check)),
      findsOneWidget,
    );
  });

  testWidgets('with nothing to switch to, it hides', (tester) async {
    await tester.pumpWidget(_switcher(spaces: const [_home]));

    expect(find.byTooltip('Switch Space'), findsNothing);
  });
}
