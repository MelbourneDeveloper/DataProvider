(function() {
  'use strict';

  const examples = {
    simple: 'users |> select(users.id, users.name, users.email)',
    join: 'users\n|> join(orders, on = users.id = orders.user_id)\n|> select(users.name, orders.total, orders.status)',
    filter: 'employees\n|> select(employees.id, employees.name, employees.salary)\n|> filter(fn(row) => row.employees.salary > 50000 and row.employees.department = \'Engineering\')',
    aggregate: 'orders\n|> group_by(orders.user_id)\n|> select(\n    orders.user_id,\n    count(*) as order_count,\n    sum(orders.total) as total_amount,\n    avg(orders.total) as avg_amount\n)\n|> having(fn(group) => count(*) > 2)\n|> order_by(total_amount desc)',
    complex: '-- Complex analytics query\nlet joined =\n    users\n    |> join(orders, on = users.id = orders.user_id)\n    |> filter(fn(row) => row.orders.status = \'completed\')\n\njoined\n|> group_by(users.id)\n|> select(\n    users.name,\n    count(*) as total_orders,\n    sum(orders.total) as revenue,\n    avg(orders.total) as avg_order_value\n)\n|> filter(fn(row) => row.revenue > 1000)\n|> order_by(revenue desc)\n|> limit(10)'
  };

  const lqlInput = document.getElementById('lql-input');
  const sqlOutput = document.getElementById('sql-output');
  const errorMessage = document.getElementById('error-message');
  const convertBtn = document.getElementById('convert-btn');
  const dialectSelector = document.getElementById('dialect-selector');
  const outputTitle = document.getElementById('output-title');

  // Load default example
  lqlInput.value = examples.simple;

  // Update output title when dialect changes
  dialectSelector.addEventListener('change', function() {
    outputTitle.textContent = this.value === 'SqlServer' ? 'SQL Server Output' : 'PostgreSQL Output';
  });

  // Convert button - calls the Blazor WASM transpiler via JS interop
  convertBtn.addEventListener('click', async function() {
    const lql = lqlInput.value.trim();
    if (!lql) {
      showError('Please enter some LQL code to convert.');
      return;
    }

    convertBtn.disabled = true;
    convertBtn.textContent = 'Converting...';
    errorMessage.style.display = 'none';
    sqlOutput.textContent = 'Converting...';

    try {
      // Call the Blazor WASM transpiler if available
      if (window.lqlTranspile) {
        const dialect = dialectSelector.value;
        const result = await window.lqlTranspile(lql, dialect);
        if (result.error) {
          showError(result.error);
          sqlOutput.textContent = '';
        } else {
          sqlOutput.textContent = result.sql;
        }
      } else {
        // Fallback: show a message that the transpiler is loading or unavailable
        sqlOutput.textContent = 'The LQL transpiler is loading. Please wait a moment and try again.\n\nIf this persists, the Blazor WASM runtime may not be available.';
      }
    } catch (err) {
      showError('An unexpected error occurred: ' + err.message);
      sqlOutput.textContent = '';
    } finally {
      convertBtn.disabled = false;
      convertBtn.textContent = 'Convert to SQL';
    }
  });

  // Example buttons
  document.querySelectorAll('.example-btn[data-example]').forEach(function(btn) {
    btn.addEventListener('click', function() {
      const key = this.getAttribute('data-example');
      if (examples[key]) {
        lqlInput.value = examples[key];
        errorMessage.style.display = 'none';
        sqlOutput.textContent = "Click 'Convert to SQL' to see the result.";
      }
    });
  });

  function showError(msg) {
    errorMessage.textContent = msg;
    errorMessage.style.display = 'block';
  }
})();
