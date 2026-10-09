import 'package:flutter/material.dart';
import 'package:weaver/app.dart';
import 'package:weaver/core/di/injection.dart';

void main() {
  configureDependencies();
  runApp(const WeaverApp());
}
