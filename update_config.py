import os

config_path = r'c:\Users\enkwi\source\repos\OnCall\Web.config'
with open(config_path, 'r') as f:
    content = f.read()

conn_string = r"""  <connectionStrings>
    <add name="OnCallContext" connectionString="Data Source=EUGINE-LEN\MSSQLSERVERX;Initial Catalog=OnCallDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;" providerName="System.Data.SqlClient" />
  </connectionStrings>
"""

if '<connectionStrings>' not in content:
    new_content = content.replace('<appSettings>', conn_string + '  <appSettings>')
    with open(config_path, 'w') as f:
        f.write(new_content)
    print("Connection string added successfully.")
else:
    print("Connection string already exists.")
