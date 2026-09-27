import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/hierarchy/hierarchy_layout.dart';

void main() {
  test('hierarchyTitleIndent indents one step per level', () {
    expect(hierarchyTitleIndent(0, 280), 0);
    expect(hierarchyTitleIndent(2, 280), 2 * hierarchyIndentPerLevel);
  });

  test('hierarchyTitleIndent keeps the caret inside the Title column', () {
    const titleWidth = 100.0;

    expect(
      hierarchyTitleIndent(50, titleWidth),
      titleWidth - caretColumnWidth - caretGap,
    );
  });

  test('hierarchyTitleIndent never goes negative for a very narrow column', () {
    expect(hierarchyTitleIndent(3, 10), 0);
  });
}
