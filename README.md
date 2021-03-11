
1. 接收来自境内的 post /jobs 请求，尝试检查上游仓库中是否存在指定的镜像，如果有，则返回 manifest，并立即使 job 入队，等待同步

2. 接收来自境内的请求 get /jobs，以显示当前队列执行情况（无详情）

3. 接收来自境外的 sync-worker 的请求 post /workers/，用于创建一个新的 available worker 记录，（同时，带回上次已完成的 jobid）服务器可以下发下一个需要拉取的镜像。如果暂时没有，则先不返回请求，持续等待3分钟，以便在3分钟之内如果有任何新镜像请求到来，则立即释放此 job。将此 job 操作标记为进行中，记录启动时间

4. 如果一个 job 超时，则立即下发给下一个 worker（超时公式为：timeout = (total Size / 100MB) * 512s ），每 100 M 最多可以传输约 8.5 分钟。