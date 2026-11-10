import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_dependency_arrows.dart';

const _neutral = Color(0xFF777777);
const _conflict = Color(0xFFB00020);

void main() {
  late ScrollController scroll;

  setUp(() => scroll = ScrollController());
  tearDown(() => scroll.dispose());

  Widget arrows(bool conflicts) => Directionality(
    textDirection: TextDirection.ltr,
    child: CustomPaint(
      size: const Size(200, 200),
      painter: RoadmapDependencyArrowsPainter(
        arrows: [
          (
            from: const Offset(30, 20),
            to: const Offset(60, 100),
            conflicts: conflicts,
          ),
        ],
        rowHeight: 40,
        scroll: scroll,
        neutralColor: _neutral,
        conflictColor: _conflict,
      ),
    ),
  );

  testWidgets('a conflicting link is drawn in the conflict colour', (
    tester,
  ) async {
    await tester.pumpWidget(arrows(true));

    expect(
      find.byType(CustomPaint),
      paints
        ..path(color: _conflict, style: PaintingStyle.stroke)
        ..path(color: _conflict, style: PaintingStyle.fill),
    );
  });

  testWidgets('any other link is drawn in the neutral colour', (tester) async {
    await tester.pumpWidget(arrows(false));

    expect(
      find.byType(CustomPaint),
      paints
        ..path(color: _neutral, style: PaintingStyle.stroke)
        ..path(color: _neutral, style: PaintingStyle.fill),
    );
  });
}
