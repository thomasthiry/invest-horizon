import { AppShell as MantineAppShell, NavLink, Group, Text, Button, Stack } from '@mantine/core';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';

export function AppShell({ children }: { children: React.ReactNode }) {
  const { email, logout } = useAuth();
  const location = useLocation();

  return (
    <MantineAppShell
      header={{ height: 50 }}
      navbar={{ width: 200, breakpoint: 'sm' }}
      padding="md"
    >
      <MantineAppShell.Header p="xs">
        <Group justify="space-between" h="100%">
          <Text fw={700} size="lg">InvestHorizon</Text>
          <Group>
            <Text size="sm" c="dimmed">{email}</Text>
            <Button size="xs" variant="subtle" onClick={logout}>Sign out</Button>
          </Group>
        </Group>
      </MantineAppShell.Header>

      <MantineAppShell.Navbar p="md">
        <Stack gap="xs">
          <NavLink label="Dashboard" component={Link} to="/" active={location.pathname === '/'} />
          <NavLink label="Securities" component={Link} to="/instruments" active={location.pathname === '/instruments'} />
          <NavLink label="Recommendations" component={Link} to="/recommendations" active={location.pathname === '/recommendations'} />
        </Stack>
      </MantineAppShell.Navbar>

      <MantineAppShell.Main>{children}</MantineAppShell.Main>
    </MantineAppShell>
  );
}
