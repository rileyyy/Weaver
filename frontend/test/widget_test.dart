import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/app.dart';
import 'package:weaver/core/di/injection.dart';

void main() {
  setUp(() async {
    await getIt.reset();
    configureDependencies();
  });

  testWidgets('renders the board with its swimlanes and columns', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(const WeaverApp());
    await tester.pumpAndSettle();

    expect(find.text('Weaver'), findsOneWidget);
    expect(find.text('To Do'), findsOneWidget);
    expect(find.text('Swimlane board'), findsOneWidget);
    expect(find.text('Design swimlane layout'), findsOneWidget);
  });
}
