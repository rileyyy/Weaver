import 'package:flutter_test/flutter_test.dart';

import 'package:weaver/app.dart';
import 'package:weaver/core/di/injection.dart';

void main() {
  setUp(() async {
    await getIt.reset();
    configureDependencies();
  });

  testWidgets('renders the app shell', (WidgetTester tester) async {
    await tester.pumpWidget(const WeaverApp());

    expect(find.text('Weaver'), findsOneWidget);
  });
}
