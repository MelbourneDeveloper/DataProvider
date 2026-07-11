SELECT employees.id, employees.name, employees.salary, employees.age, employees.department_id
FROM employees
WHERE employees.salary >= 50000 AND employees.salary <= 150000 AND employees.age > 25 AND employees.age < 65 AND employees.department_id != 999 AND employees.salary <> 0