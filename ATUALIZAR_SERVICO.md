# Atualizar Serviço Systemd Manualmente

Se o arquivo de serviço no servidor ainda estiver com o caminho errado, execute:

```bash
sudo nano /etc/systemd/system/datamais.service
```

Altere a linha:
```
ExecStart=/usr/bin/dotnet /home/becape/datamais.api/DataMais.dll
```

Para:
```
ExecStart=/home/becape/.dotnet/dotnet /home/becape/datamais.api/DataMais.dll
```

Depois:
```bash
sudo systemctl daemon-reload
sudo systemctl restart datamais.service
sudo systemctl status datamais.service
```

Ou execute diretamente:
```bash
sudo sed -i 's|ExecStart=/usr/bin/dotnet|ExecStart=/home/becape/.dotnet/dotnet|' /etc/systemd/system/datamais.service
sudo systemctl daemon-reload
sudo systemctl restart datamais.service
```
