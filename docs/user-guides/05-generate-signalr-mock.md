# Generate SignalR Mock Command

The `signalr-mock` command generates a **fully functional SignalR mock fixture** using RxJS (not promises). This mock is designed for testing Angular applications that use SignalR for real-time communication.

## Command Syntax

```bash
playwright-pom-gen signalr-mock <output> [options]
```

## Arguments

### `output` (Required)
The output directory for the generated SignalR mock file.

- **Type:** String (positional argument)
- **Default:** `.` (current directory) if not specified
- **Description:** Directory where the SignalR mock fixture will be generated

**Examples:**
```bash
# Current directory
playwright-pom-gen signalr-mock .

# Specific directory
playwright-pom-gen signalr-mock ./fixtures

# Nested directory
playwright-pom-gen signalr-mock ./e2e/mocks

# Absolute path (Windows)
playwright-pom-gen signalr-mock C:\projects\my-app\tests\fixtures

# Absolute path (Linux/macOS)
playwright-pom-gen signalr-mock /home/user/projects/my-app/tests/fixtures
```

## Options

### Global Options

See [Global Options Guide](07-global-options.md) for options available to all commands:
- `--header` - Custom file header template

**Note:** `--test-suffix` is not applicable to this command as it doesn't generate test files.

## When to Use

Use the `signalr-mock` command when:

- ✅ Your Angular application uses **SignalR** for real-time communication
- ✅ You need to **mock SignalR connections** in Playwright tests
- ✅ You want **RxJS-based observables** (not promise-based mocks)
- ✅ You need to **simulate server messages** and connection states
- ✅ You want to **test SignalR interactions** without a real SignalR server

**Don't use when:**
- ❌ Your application doesn't use SignalR
- ❌ You can use a real SignalR server in tests
- ❌ You need a promise-based mock (this is RxJS-based)

## What Is Generated

The command generates a single, comprehensive SignalR mock fixture file:

### File: `signalr.mock.fixture.ts`

A complete SignalR mock implementation with:
- **RxJS Observable Streams** - Not promises, proper reactive streams
- **Connection State Management** - Connecting, Connected, Reconnecting, Disconnected
- **Method Invocation Tracking** - Track all methods called with arguments
- **Server Message Simulation** - Emit server messages to connected clients
- **Error Simulation** - Simulate connection errors and failures
- **Reconnection Simulation** - Test reconnection logic
- **TypeScript Support** - Fully typed for TypeScript projects

## Features of the Generated Mock

### 1. RxJS-Based (Not Promises)

The mock uses RxJS observables, matching how SignalR works in Angular:

```typescript
// Generated mock provides observables
connection.on('MessageReceived').subscribe(message => {
  // Handle message
});

// Not promise-based
// connection.on('MessageReceived').then(...) ❌
```

### 2. Connection State Management

Track and simulate connection states:

```typescript
// States: Connecting, Connected, Reconnecting, Disconnected
mock.connectionState$.subscribe(state => {
  console.log('Connection state:', state);
});

// Simulate state changes
mock.connect();          // → Connected
mock.disconnect();       // → Disconnected
mock.simulateError();    // → Reconnecting
```

### 3. Method Invocation Tracking

Track all methods called on the connection:

```typescript
// Call a method
await connection.invoke('SendMessage', 'Hello');

// Assert it was called
expect(mock.invokedMethods).toContain('SendMessage');
expect(mock.getInvocations('SendMessage')).toHaveLength(1);
```

### 4. Server Message Simulation

Simulate server-side messages:

```typescript
// Set up listener in app
connection.on('UserJoined').subscribe(user => {
  console.log('User joined:', user);
});

// Simulate server sending message in test
mock.emitServerMessage('UserJoined', { name: 'Alice' });
```

### 5. Error Simulation

Test error handling:

```typescript
// Simulate connection error
mock.simulateError(new Error('Connection lost'));

// Simulate reconnection
mock.simulateReconnect();
```

### 6. Complete API Coverage

Mocks all SignalR client methods:
- `start()` / `stop()`
- `on()` / `off()`
- `send()` / `invoke()`
- `stream()`
- Connection state events

## How It Works

### Step 1: Generation
1. Creates the SignalR mock fixture file
2. Includes all necessary RxJS imports
3. Provides complete SignalR API mock
4. Adds TypeScript type definitions

### Step 2: Integration
You integrate the mock into your Playwright fixtures:

```typescript
// In your test fixture
import { test as base } from '@playwright/test';
import { SignalRMock } from './fixtures/signalr.mock.fixture';

export const test = base.extend({
  signalRMock: async ({ page }, use) => {
    const mock = new SignalRMock();
    
    // Inject mock into page
    await page.addInitScript(() => {
      window.signalRMock = mock;
    });
    
    await use(mock);
  }
});
```

### Step 3: Usage in Tests
Use the mock in your tests:

```typescript
test('should handle SignalR messages', async ({ page, signalRMock }) => {
  // Navigate to app
  await page.goto('/');
  
  // App connects to SignalR (using mock)
  signalRMock.connect();
  
  // Simulate server message
  signalRMock.emitServerMessage('Notification', { 
    text: 'New message!' 
  });
  
  // Assert UI updated
  await expect(page.locator('.notification')).toHaveText('New message!');
});
```

## Examples

### Example 1: Basic Generation

Generate SignalR mock in current directory:

```bash
playwright-pom-gen signalr-mock .
```

**Output:**
```
info: Generating SignalR mock fixture at .
Successfully generated SignalR mock fixture:
  - C:\projects\my-app\signalr.mock.fixture.ts

The mock provides:
  - RxJS-based observable streams (not promises)
  - Connection state management
  - Method invocation tracking
  - Server message simulation
  - Error simulation
  - Reconnection simulation
```

**Exit code:** 0 (success)

### Example 2: Generate in Fixtures Directory

```bash
playwright-pom-gen signalr-mock ./e2e/fixtures
```

**Result:**
- File created: `./e2e/fixtures/signalr.mock.fixture.ts`

### Example 3: With Custom Header

```bash
playwright-pom-gen signalr-mock ./fixtures \
  --header "// Copyright 2026 ACME Corp\n// SignalR Mock - DO NOT EDIT"
```

**Result:**
- File includes custom header at the top

### Example 4: Complete Test Setup

```bash
# 1. Generate the mock
playwright-pom-gen signalr-mock ./e2e/fixtures

# 2. Create test fixture that uses it
cat > ./e2e/fixtures/app.fixture.ts << 'EOF'
import { test as base } from '@playwright/test';
import { SignalRMock } from './signalr.mock.fixture';

type AppFixtures = {
  signalRMock: SignalRMock;
};

export const test = base.extend<AppFixtures>({
  signalRMock: async ({ page }, use) => {
    const mock = new SignalRMock();
    
    await page.addInitScript((mockObj) => {
      // Replace real SignalR with mock
      window.signalR = mockObj;
    }, mock);
    
    await use(mock);
  }
});

export { expect } from '@playwright/test';
EOF

# 3. Write a test
cat > ./e2e/tests/signalr.spec.ts << 'EOF'
import { test, expect } from '../fixtures/app.fixture';

test('should receive SignalR notifications', async ({ page, signalRMock }) => {
  await page.goto('/dashboard');
  
  // Wait for app to connect
  await page.waitForSelector('[data-connected="true"]');
  
  // Simulate server notification
  signalRMock.emitServerMessage('NotificationReceived', {
    title: 'Test Notification',
    message: 'Hello from mock!'
  });
  
  // Assert UI shows notification
  await expect(page.locator('.notification-title')).toHaveText('Test Notification');
  await expect(page.locator('.notification-message')).toHaveText('Hello from mock!');
});
EOF
```

### Example 5: Test Hub Method Invocations

```bash
# After generating the mock, use it to test method calls
```

**Test file:**
```typescript
test('should invoke SignalR hub methods', async ({ page, signalRMock }) => {
  await page.goto('/chat');
  
  // User sends a message
  await page.fill('[data-testid="message-input"]', 'Hello');
  await page.click('[data-testid="send-button"]');
  
  // Assert SignalR method was called
  const invocations = signalRMock.getInvocations('SendMessage');
  expect(invocations).toHaveLength(1);
  expect(invocations[0].args).toEqual(['Hello']);
});
```

### Example 6: Test Connection States

**Test file:**
```typescript
test('should handle connection states', async ({ page, signalRMock }) => {
  await page.goto('/');
  
  // Initial state: disconnected
  await expect(page.locator('[data-status]')).toHaveAttribute('data-status', 'disconnected');
  
  // Simulate connection
  signalRMock.connect();
  await expect(page.locator('[data-status]')).toHaveAttribute('data-status', 'connected');
  
  // Simulate disconnection
  signalRMock.disconnect();
  await expect(page.locator('[data-status]')).toHaveAttribute('data-status', 'disconnected');
  
  // Simulate error and reconnection
  signalRMock.simulateError(new Error('Network error'));
  await expect(page.locator('[data-status]')).toHaveAttribute('data-status', 'reconnecting');
  
  signalRMock.simulateReconnect();
  await expect(page.locator('[data-status]')).toHaveAttribute('data-status', 'connected');
});
```

## Mock API Reference

The generated mock provides the following API:

### Properties

```typescript
class SignalRMock {
  // Connection state observable
  connectionState$: BehaviorSubject<ConnectionState>;
  
  // All methods invoked on the connection
  invokedMethods: string[];
  
  // Detailed invocation records
  invocations: Map<string, MethodInvocation[]>;
}
```

### Methods

```typescript
// Connection management
connect(): void
disconnect(): void
stop(): Promise<void>

// Event handling
on(eventName: string): Observable<any>
off(eventName: string): void

// Method invocation
send(methodName: string, ...args: any[]): Promise<void>
invoke(methodName: string, ...args: any[]): Promise<any>
stream(methodName: string, ...args: any[]): Observable<any>

// Test utilities
emitServerMessage(eventName: string, data: any): void
simulateError(error: Error): void
simulateReconnect(): void
getInvocations(methodName: string): MethodInvocation[]
clearInvocations(): void
```

### Types

```typescript
enum ConnectionState {
  Disconnected = 'Disconnected',
  Connecting = 'Connecting',
  Connected = 'Connected',
  Reconnecting = 'Reconnecting'
}

interface MethodInvocation {
  methodName: string;
  args: any[];
  timestamp: Date;
}
```

## Integration Patterns

### Pattern 1: Global Mock Setup

Replace SignalR globally for all tests:

```typescript
// global-setup.ts
import { chromium, FullConfig } from '@playwright/test';

async function globalSetup(config: FullConfig) {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  
  // Inject mock globally
  await page.addInitScript(() => {
    // Mock SignalR at window level
    window.signalR = {
      HubConnectionBuilder: class MockHubConnectionBuilder {
        // ... mock implementation
      }
    };
  });
  
  await browser.close();
}

export default globalSetup;
```

### Pattern 2: Per-Test Mock Control

Control mock behavior per test:

```typescript
test('with delayed connection', async ({ page, signalRMock }) => {
  await page.goto('/');
  
  // Delay connection by 2 seconds
  setTimeout(() => signalRMock.connect(), 2000);
  
  // Assert loading state
  await expect(page.locator('.loading')).toBeVisible();
  
  // Wait for connection
  await page.waitForSelector('[data-connected="true"]');
});
```

### Pattern 3: Mock Multiple Hubs

Test apps with multiple SignalR hubs:

```typescript
export const test = base.extend({
  chatMock: async ({ page }, use) => {
    const mock = new SignalRMock();
    await page.addInitScript((m) => {
      window.chatHub = m;
    }, mock);
    await use(mock);
  },
  
  notificationMock: async ({ page }, use) => {
    const mock = new SignalRMock();
    await page.addInitScript((m) => {
      window.notificationHub = m;
    }, mock);
    await use(mock);
  }
});
```

## Use Cases

### Use Case 1: Test Real-Time Notifications

```typescript
test('displays real-time notifications', async ({ page, signalRMock }) => {
  await page.goto('/');
  
  // Simulate server notification
  signalRMock.emitServerMessage('NotificationReceived', {
    type: 'info',
    message: 'System update available'
  });
  
  // Verify notification appears
  await expect(page.locator('.toast')).toBeVisible();
  await expect(page.locator('.toast')).toHaveText('System update available');
});
```

### Use Case 2: Test Chat Application

```typescript
test('sends and receives chat messages', async ({ page, signalRMock }) => {
  await page.goto('/chat');
  
  // Send message
  await page.fill('#message', 'Hello everyone!');
  await page.click('#send');
  
  // Verify method invoked
  expect(signalRMock.getInvocations('SendMessage')).toHaveLength(1);
  
  // Simulate receiving message from another user
  signalRMock.emitServerMessage('MessageReceived', {
    user: 'Alice',
    message: 'Hi there!'
  });
  
  // Verify message appears in chat
  await expect(page.locator('.chat-message').last()).toContainText('Hi there!');
});
```

### Use Case 3: Test Connection Loss Recovery

```typescript
test('recovers from connection loss', async ({ page, signalRMock }) => {
  await page.goto('/');
  signalRMock.connect();
  
  // Verify connected state
  await expect(page.locator('.status')).toHaveText('Connected');
  
  // Simulate connection error
  signalRMock.simulateError(new Error('Network timeout'));
  
  // Verify reconnecting state
  await expect(page.locator('.status')).toHaveText('Reconnecting...');
  
  // Simulate reconnection
  signalRMock.simulateReconnect();
  
  // Verify restored
  await expect(page.locator('.status')).toHaveText('Connected');
});
```

### Use Case 4: Test Live Data Updates

```typescript
test('updates dashboard with live data', async ({ page, signalRMock }) => {
  await page.goto('/dashboard');
  
  // Simulate live data updates
  signalRMock.emitServerMessage('StockPriceUpdated', {
    symbol: 'AAPL',
    price: 150.25
  });
  
  // Verify UI updated
  await expect(page.locator('[data-symbol="AAPL"]')).toHaveText('$150.25');
  
  // Another update
  signalRMock.emitServerMessage('StockPriceUpdated', {
    symbol: 'AAPL',
    price: 151.00
  });
  
  await expect(page.locator('[data-symbol="AAPL"]')).toHaveText('$151.00');
});
```

## RxJS vs Promises

**Why RxJS?**

SignalR's TypeScript client uses observables for event streams, not promises:

✅ **Correct (RxJS):**
```typescript
connection.on('MessageReceived').subscribe(msg => {
  // Handle message
});
```

❌ **Incorrect (Promises):**
```typescript
connection.on('MessageReceived').then(msg => {
  // This doesn't work with SignalR
});
```

The generated mock uses RxJS to match this behavior, ensuring your tests accurately reflect how your app uses SignalR.

## Troubleshooting

### Issue: Mock Not Working in Tests

**Symptoms:**
- Real SignalR is still being used
- Mock methods not being called

**Solution:**
```typescript
// Ensure mock is injected before app loads
test.beforeEach(async ({ page, signalRMock }) => {
  // Inject BEFORE navigating
  await page.addInitScript((mock) => {
    window.signalR = mock;
  }, signalRMock);
  
  // THEN navigate
  await page.goto('/');
});
```

### Issue: TypeScript Errors with Mock

**Symptoms:**
- Type errors when using mock
- Properties not recognized

**Solution:**
```typescript
// Add type declaration
declare global {
  interface Window {
    signalR: SignalRMock;
  }
}
```

### Issue: Messages Not Received

**Symptoms:**
- `emitServerMessage()` doesn't trigger handlers
- UI doesn't update

**Debug:**
```typescript
test('debug message emission', async ({ page, signalRMock }) => {
  // Set up listener
  signalRMock.on('TestEvent').subscribe(data => {
    console.log('Received:', data);
  });
  
  // Emit message
  signalRMock.emitServerMessage('TestEvent', { test: 'data' });
  
  // Should log: Received: { test: 'data' }
});
```

## Best Practices

1. **Generate once, commit to repo**
   ```bash
   playwright-pom-gen signalr-mock ./e2e/fixtures
   git add e2e/fixtures/signalr.mock.fixture.ts
   git commit -m "Add SignalR mock fixture"
   ```

2. **Create wrapper fixture for easy use**
   ```typescript
   // app.fixture.ts
   export const test = base.extend({
     signalR: async ({ page }, use) => {
       const mock = new SignalRMock();
       // ... setup ...
       await use(mock);
     }
   });
   ```

3. **Reset mock between tests**
   ```typescript
   test.afterEach(async ({ signalRMock }) => {
     signalRMock.clearInvocations();
     signalRMock.disconnect();
   });
   ```

4. **Test both happy and error paths**
   ```typescript
   test('handles connection errors gracefully', async ({ signalRMock }) => {
     signalRMock.simulateError(new Error('Connection failed'));
     // ... assert error handling ...
   });
   ```

## Next Steps

- Integrate the mock into your [test fixtures](08-output-structure.md#fixtures)
- Learn about [Best Practices](10-best-practices.md) for mocking
- Understand [Configuration](06-configuration.md) for customizing the generated mock
- Review Playwright's documentation on [test fixtures](https://playwright.dev/docs/test-fixtures)

## Related Documentation

- [Playwright Test Fixtures](https://playwright.dev/docs/test-fixtures)
- [RxJS Documentation](https://rxjs.dev/)
- [SignalR TypeScript Client](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client)
