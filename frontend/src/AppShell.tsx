import { AppShell as MantineAppShell, NavLink, Group, Text, Button, Stack, Burger } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';

export function AppShell({ children }: { children: React.ReactNode }) {
  const { email, logout } = useAuth();
  const location = useLocation();
  const [opened, { toggle, close }] = useDisclosure();

  return (
    <MantineAppShell
      header={{ height: 50 }}
      navbar={{ width: 200, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding="md"
    >
      <MantineAppShell.Header p="xs">
        <Group justify="space-between" h="100%">
          <Group gap="sm">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" />
            <Text fw={700} size="lg">InvestHorizon</Text>
          </Group>
          <Group>
            <Text size="sm" c="dimmed" visibleFrom="sm">{email}</Text>
            <Button size="xs" variant="subtle" onClick={logout}>Sign out</Button>
          </Group>
        </Group>
      </MantineAppShell.Header>

      <MantineAppShell.Navbar p="md">
        <Stack gap="xs">
          <NavLink label="Dashboard" component={Link} to="/" active={location.pathname === '/'} onClick={close} />
          <NavLink label="Securities" component={Link} to="/instruments" active={location.pathname === '/instruments'} onClick={close} />
          <NavLink label="Recommendations" component={Link} to="/recommendations" active={location.pathname === '/recommendations'} onClick={close} />
        </Stack>
      </MantineAppShell.Navbar>

      <MantineAppShell.Main>{children}</MantineAppShell.Main>
    </MantineAppShell>
  );
}
